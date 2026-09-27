using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class PaymentFailureCompensationTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 27, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InventoryReleasedThenOrderCancelledCompletesBusinessFailure()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedCompensatingInventorySagaAsync(
            orderId,
            reservationId,
            paymentId,
            cancellationToken);

        await using (var dbContext = fixture.CreateDbContext())
        {
            var processor =
                new InventoryReleasedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new InventoryReleasedMessageHandler(
                        new CheckoutSagaRepository(dbContext),
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            var occurredAtUtc = StartedAtUtc.AddSeconds(4);

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedInventoryReleasedMessage(
                        new InventoryReleasedMessage(
                            new IntegrationMessageEnvelope(
                                Guid.NewGuid(),
                                "InventoryReleased.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                occurredAtUtc,
                                "Inventory",
                                null),
                            new InventoryReleasedV1(
                                orderId,
                                reservationId,
                                occurredAtUtc,
                                "PAYMENT_FAILED")),
                        "inventory.events",
                        0,
                        950,
                        occurredAtUtc),
                    occurredAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var saga =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        entity => entity.OrderId == orderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.WaitingForOrderCancellation.ToString(),
                saga.Status);

            Assert.Equal(
                1,
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .CountAsync(
                        message =>
                            message.AggregateId == orderId
                            && message.MessageType == "CancelOrder.v1",
                        cancellationToken));
        }

        await using (var dbContext = fixture.CreateDbContext())
        {
            var occurredAtUtc = StartedAtUtc.AddSeconds(6);

            var processor =
                new OrderCancelledInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new OrderCancelledMessageHandler(
                        new CheckoutSagaRepository(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedOrderCancelledMessage(
                        new OrderCancelledMessage(
                            new IntegrationMessageEnvelope(
                                Guid.NewGuid(),
                                "OrderCancelled.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                occurredAtUtc,
                                "Order",
                                null),
                            new OrderCancelledV1(
                                orderId,
                                "PAYMENT_FAILED",
                                occurredAtUtc)),
                        "orders.events",
                        0,
                        951,
                        occurredAtUtc),
                    occurredAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        await using var finalDbContext =
            fixture.CreateDbContext();

        var completedSaga =
            await finalDbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.CompletedWithBusinessFailure.ToString(),
            completedSaga.Status);
    }

    private async Task SeedCompensatingInventorySagaAsync(
        Guid orderId,
        Guid reservationId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var deadlineAtUtc = StartedAtUtc.AddMinutes(30);

        var saga =
            CheckoutSaga.Start(
                orderId,
                Guid.NewGuid(),
                [
                    CheckoutSagaItem.Create(
                        "SKU-001",
                        1,
                        35m,
                        "USD")
                ],
                "USD",
                35m,
                StartedAtUtc,
                deadlineAtUtc);

        saga.BeginInventoryReservation(
            reservationId,
            StartedAtUtc.AddSeconds(1),
            deadlineAtUtc);
        saga.ConfirmInventoryReserved(
            reservationId,
            paymentId,
            StartedAtUtc.AddSeconds(2));
        saga.RejectPaymentCapture(
            paymentId,
            StartedAtUtc.AddSeconds(3));

        await using var dbContext =
            fixture.CreateDbContext();

        await new CheckoutSagaRepository(dbContext)
            .AddAsync(saga, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
