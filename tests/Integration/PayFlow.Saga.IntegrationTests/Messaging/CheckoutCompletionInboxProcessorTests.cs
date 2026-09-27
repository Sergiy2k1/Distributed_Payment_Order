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

public sealed class CheckoutCompletionInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 27, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public async Task InventoryConsumedThenOrderConfirmedCompletesSaga()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedWaitingForInventoryCommitSagaAsync(
            orderId,
            reservationId,
            paymentId,
            cancellationToken);

        var inventoryMessageId = Guid.NewGuid();

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var processor =
                new InventoryConsumedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new InventoryConsumedMessageHandler(
                        new CheckoutSagaRepository(dbContext),
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedInventoryConsumedMessage(
                        new InventoryConsumedMessage(
                            new IntegrationMessageEnvelope(
                                inventoryMessageId,
                                "InventoryConsumed.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(4),
                                "Inventory",
                                null),
                            new InventoryConsumedV1(
                                orderId,
                                reservationId,
                                StartedAtUtc.AddSeconds(4))),
                        "inventory.events",
                        0,
                        900,
                        StartedAtUtc.AddSeconds(4)),
                    StartedAtUtc.AddSeconds(5),
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
                CheckoutSagaStatus.WaitingForOrderConfirmation.ToString(),
                saga.Status);

            var confirmOutbox =
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(
                        message =>
                            message.AggregateId == orderId
                            && message.MessageType == "ConfirmOrder.v1",
                        cancellationToken);

            Assert.Equal(
                "orders.commands",
                confirmOutbox.Destination);
            Assert.Equal(
                inventoryMessageId,
                confirmOutbox.CausationId);
        }

        var orderConfirmedMessageId = Guid.NewGuid();

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var processor =
                new OrderConfirmedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new OrderConfirmedMessageHandler(
                        new CheckoutSagaRepository(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedOrderConfirmedMessage(
                        new OrderConfirmedMessage(
                            new IntegrationMessageEnvelope(
                                orderConfirmedMessageId,
                                "OrderConfirmed.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(6),
                                "Order",
                                null),
                            new OrderConfirmedV1(
                                orderId,
                                StartedAtUtc.AddSeconds(6))),
                        "orders.events",
                        0,
                        901,
                        StartedAtUtc.AddSeconds(6)),
                    StartedAtUtc.AddSeconds(7),
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
            CheckoutSagaStatus.Completed.ToString(),
            completedSaga.Status);
    }

    private async Task SeedWaitingForInventoryCommitSagaAsync(
        Guid orderId,
        Guid reservationId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var saga =
            CheckoutSaga.Start(
                orderId,
                Guid.NewGuid(),
                [
                    CheckoutSagaItem.Create(
                        "SKU-001",
                        2,
                        10m,
                        "USD"),
                    CheckoutSagaItem.Create(
                        "SKU-002",
                        3,
                        5m,
                        "USD")
                ],
                "USD",
                35m,
                StartedAtUtc,
                DeadlineAtUtc);

        saga.BeginInventoryReservation(
            reservationId,
            StartedAtUtc.AddSeconds(1),
            DeadlineAtUtc);

        saga.ConfirmInventoryReserved(
            reservationId,
            paymentId,
            StartedAtUtc.AddSeconds(2));

        saga.ConfirmPaymentCaptured(
            paymentId,
            StartedAtUtc.AddSeconds(3));

        await using var dbContext =
            fixture.CreateDbContext();

        await new CheckoutSagaRepository(dbContext)
            .AddAsync(
                saga,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
