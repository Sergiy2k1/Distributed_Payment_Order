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

public sealed class ReleaseInventoryInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset ReservedAtUtc =
        new(2026, 9, 27, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReservedInventoryIsReleasedAndPublishesEvent()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var sku = $"SKU-REL-{suffix}";
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await SeedStockAsync(
            sku,
            5,
            cancellationToken);

        await ReserveAsync(
            orderId,
            reservationId,
            sku,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var processor =
                new ReleaseInventoryInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new ReleaseInventoryMessageHandler(
                        new InventoryReservationRepository(dbContext),
                        new StockRepository(dbContext),
                        new InventoryOutboxWriter(dbContext),
                        new InventoryEfUnitOfWork(dbContext)));

            var occurredAtUtc =
                ReservedAtUtc.AddSeconds(2);

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedReleaseInventoryMessage(
                        new ReleaseInventoryMessage(
                            new IntegrationMessageEnvelope(
                                Guid.NewGuid(),
                                "ReleaseInventory.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                occurredAtUtc,
                                "Saga",
                                null),
                            new ReleaseInventoryV1(
                                orderId,
                                reservationId,
                                "PAYMENT_FAILED")),
                        "inventory.commands",
                        0,
                        30,
                        occurredAtUtc),
                    occurredAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var reservation =
            await verificationDbContext.Reservations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.ReservationId
                        == reservationId,
                    cancellationToken);

        Assert.Equal(
            InventoryReservationStatus.Released.ToString(),
            reservation.Status);

        var stock =
            await verificationDbContext.StockItems
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.SkuId == sku,
                    cancellationToken);

        Assert.Equal(5, stock.OnHand);
        Assert.Equal(0, stock.Reserved);

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType
                        == "InventoryReleased.v1",
                    cancellationToken));
    }

    private async Task ReserveAsync(
        Guid orderId,
        Guid reservationId,
        string sku,
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
                                sku,
                                2)
                        ],
                        ReservedAtUtc.AddMinutes(30))),
                "inventory.commands",
                0,
                29,
                ReservedAtUtc),
            ReservedAtUtc.AddSeconds(1),
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
