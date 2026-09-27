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

public sealed class ConsumeInventoryInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset ReservedAtUtc =
        new(2026, 9, 27, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReservedInventoryIsConsumedAndPublishesEvent()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var skuA = $"SKU-A-{suffix}";
        var skuB = $"SKU-B-{suffix}";
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await SeedStockAsync(
            skuA,
            5,
            cancellationToken);
        await SeedStockAsync(
            skuB,
            7,
            cancellationToken);

        await ReserveAsync(
            orderId,
            reservationId,
            skuA,
            skuB,
            cancellationToken);

        await ConsumeAsync(
            orderId,
            reservationId,
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var reservation =
            await dbContext.Reservations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.ReservationId
                        == reservationId,
                    cancellationToken);

        Assert.Equal(
            InventoryReservationStatus.Consumed.ToString(),
            reservation.Status);

        var stock =
            await dbContext.StockItems
                .AsNoTracking()
                .Where(entity =>
                    entity.SkuId == skuA
                    || entity.SkuId == skuB)
                .OrderBy(entity => entity.SkuId)
                .ToArrayAsync(cancellationToken);

        Assert.Equal(4, stock[0].OnHand);
        Assert.Equal(0, stock[0].Reserved);
        Assert.Equal(5, stock[1].OnHand);
        Assert.Equal(0, stock[1].Reserved);

        Assert.Equal(
            1,
            await dbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType
                        == "InventoryConsumed.v1",
                    cancellationToken));
    }

    private async Task ReserveAsync(
        Guid orderId,
        Guid reservationId,
        string skuA,
        string skuB,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var processor =
            new ReserveInventoryInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                new ReserveInventoryMessageHandler(
                    new InventoryReservationRepository(dbContext),
                    new StockRepository(dbContext),
                    new InventoryOutboxWriter(dbContext),
                    new InventoryEfUnitOfWork(dbContext)));

        await processor.ProcessAsync(
            new ConsumedReserveInventoryMessage(
                new ReserveInventoryMessage(
                    new IntegrationMessageEnvelope(
                        Guid.NewGuid(),
                        "ReserveInventory.v1",
                        1,
                        orderId,
                        orderId,
                        null,
                        ReservedAtUtc,
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
                        ReservedAtUtc.AddMinutes(30))),
                "inventory.commands",
                0,
                10,
                ReservedAtUtc),
            ReservedAtUtc.AddSeconds(1),
            cancellationToken);
    }

    private async Task ConsumeAsync(
        Guid orderId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var processor =
            new ConsumeInventoryInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                new ConsumeInventoryMessageHandler(
                    new InventoryReservationRepository(dbContext),
                    new StockRepository(dbContext),
                    new InventoryOutboxWriter(dbContext),
                    new InventoryEfUnitOfWork(dbContext)));

        var occurredAtUtc =
            ReservedAtUtc.AddSeconds(2);

        await processor.ProcessAsync(
            new ConsumedConsumeInventoryMessage(
                new ConsumeInventoryMessage(
                    new IntegrationMessageEnvelope(
                        Guid.NewGuid(),
                        "ConsumeInventory.v1",
                        1,
                        orderId,
                        orderId,
                        null,
                        occurredAtUtc,
                        "Saga",
                        null),
                    new ConsumeInventoryV1(
                        orderId,
                        reservationId)),
                "inventory.commands",
                0,
                11,
                occurredAtUtc),
            occurredAtUtc.AddSeconds(1),
            cancellationToken);
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
