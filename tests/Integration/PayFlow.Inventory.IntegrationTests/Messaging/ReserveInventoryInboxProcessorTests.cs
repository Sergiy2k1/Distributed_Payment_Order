using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Application.Messaging;
using PayFlow.Inventory.Application.Reservations;
using PayFlow.Inventory.Domain.Reservations;
using PayFlow.Inventory.Domain.Stock;
using PayFlow.Inventory.Infrastructure.Messaging;
using PayFlow.Inventory.Infrastructure.Messaging.Outbox;
using PayFlow.Inventory.Infrastructure.Persistence;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;
using PayFlow.Inventory.IntegrationTests.Infrastructure;

namespace PayFlow.Inventory.IntegrationTests.Messaging;

public sealed class ReserveInventoryInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AvailableStockCreatesReservedReservationAndOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var skuA = $"SKU-A-{suffix}";
        var skuB = $"SKU-B-{suffix}";
        var message = CreateMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            skuA,
            skuB);

        await SeedStockAsync(
            skuA,
            3,
            cancellationToken);
        await SeedStockAsync(
            skuB,
            4,
            cancellationToken);

        await ProcessAsync(
            message,
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var reservation =
            await dbContext.Reservations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.ReservationId
                        == message.Message.Payload.ReservationId,
                    cancellationToken);

        Assert.Equal(
            InventoryReservationStatus.Reserved.ToString(),
            reservation.Status);

        var stock = await dbContext.StockItems
            .AsNoTracking()
            .Where(item =>
                item.SkuId == skuA
                || item.SkuId == skuB)
            .OrderBy(item => item.SkuId)
            .ToArrayAsync(cancellationToken);

        Assert.Equal(1, stock[0].Reserved);
        Assert.Equal(2, stock[1].Reserved);

        var outbox =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.AggregateId
                        == message.Message.Payload.OrderId,
                    cancellationToken);

        Assert.Equal(
            "InventoryReserved.v1",
            outbox.MessageType);

        var payload =
            JsonSerializer.Deserialize<InventoryReservedV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(
            message.Message.Payload.ReservationId,
            payload.ReservationId);
    }

    [Fact]
    public async Task InsufficientStockRejectsWithoutPartialMutation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var skuA = $"SKU-A-{suffix}";
        var skuB = $"SKU-B-{suffix}";
        var message = CreateMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            skuA,
            skuB);

        await SeedStockAsync(
            skuA,
            5,
            cancellationToken);
        await SeedStockAsync(
            skuB,
            1,
            cancellationToken);

        await ProcessAsync(
            message,
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var stock = await dbContext.StockItems
            .AsNoTracking()
            .Where(item =>
                item.SkuId == skuA
                || item.SkuId == skuB)
            .ToArrayAsync(cancellationToken);

        Assert.All(
            stock,
            item => Assert.Equal(0, item.Reserved));

        var reservation =
            await dbContext.Reservations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.ReservationId
                        == message.Message.Payload.ReservationId,
                    cancellationToken);

        Assert.Equal(
            InventoryReservationStatus.Rejected.ToString(),
            reservation.Status);
        Assert.Equal(
            ReserveInventoryMessageHandler.InsufficientStockReason,
            reservation.RejectionReasonCode);

        Assert.Equal(
            1,
            await dbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.MessageType
                        == "InventoryReservationRejected.v1"
                        && entity.AggregateId
                        == message.Message.Payload.OrderId,
                    cancellationToken));
    }

    [Fact]
    public async Task SameReservationIdWithNewMessageIdDoesNotReserveTwice()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var skuA = $"SKU-A-{suffix}";
        var skuB = $"SKU-B-{suffix}";
        var reservationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await SeedStockAsync(
            skuA,
            10,
            cancellationToken);
        await SeedStockAsync(
            skuB,
            10,
            cancellationToken);

        await ProcessAsync(
            CreateMessage(
                orderId,
                reservationId,
                skuA,
                skuB),
            cancellationToken);

        await ProcessAsync(
            CreateMessage(
                orderId,
                reservationId,
                skuA,
                skuB),
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var stock = await dbContext.StockItems
            .AsNoTracking()
            .Where(item =>
                item.SkuId == skuA
                || item.SkuId == skuB)
            .OrderBy(item => item.SkuId)
            .ToArrayAsync(cancellationToken);

        Assert.Equal(1, stock[0].Reserved);
        Assert.Equal(2, stock[1].Reserved);

        Assert.Equal(
            1,
            await dbContext.Reservations
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.ReservationId
                        == reservationId,
                    cancellationToken));

        Assert.Equal(
            1,
            await dbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId,
                    cancellationToken));
    }

    private async Task ProcessAsync(
        ConsumedReserveInventoryMessage message,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var handler =
            new ReserveInventoryMessageHandler(
                new InventoryReservationRepository(dbContext),
                new StockRepository(dbContext),
                new InventoryOutboxWriter(dbContext),
                new InventoryEfUnitOfWork(dbContext));

        var processor =
            new ReserveInventoryInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                handler);

        await processor.ProcessAsync(
            message,
            OccurredAtUtc.AddSeconds(1),
            cancellationToken);
    }

    private static ConsumedReserveInventoryMessage CreateMessage(
        Guid orderId,
        Guid reservationId,
        string skuA,
        string skuB)
    {
        return new ConsumedReserveInventoryMessage(
            new ReserveInventoryMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "ReserveInventory.v1",
                    1,
                    orderId,
                    orderId,
                    null,
                    OccurredAtUtc,
                    "Saga",
                    null),
                new ReserveInventoryV1(
                    orderId,
                    reservationId,
                    [
                        new ReserveInventoryItemV1(
                            skuA,
                            1),
                        new ReserveInventoryItemV1(
                            skuB,
                            2)
                    ],
                    OccurredAtUtc.AddMinutes(30))),
            "inventory.commands",
            0,
            1,
            OccurredAtUtc);
    }

    private async Task SeedStockAsync(
        string skuId,
        int onHand,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        await new StockRepository(dbContext)
            .AddAsync(
                StockItem.Create(
                    skuId,
                    onHand),
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
