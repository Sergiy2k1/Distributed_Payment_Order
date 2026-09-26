using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Payments;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class InventoryResultInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 26, 22, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public async Task InventoryReservedMovesSagaAndEnqueuesCapturePayment()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedWaitingForInventorySagaAsync(
            orderId,
            reservationId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(dbContext);

            var processor =
                new InventoryReservedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new InventoryReservedMessageHandler(
                        repository,
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedInventoryReservedMessage(
                        new InventoryReservedMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "InventoryReserved.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(2),
                                "Inventory",
                                null),
                            new InventoryReservedV1(
                                orderId,
                                reservationId,
                                StartedAtUtc.AddSeconds(2),
                                DeadlineAtUtc)),
                        "inventory.events",
                        0,
                        700,
                        StartedAtUtc.AddSeconds(2)),
                    StartedAtUtc.AddSeconds(3),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var saga =
            await verificationDbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForPayment.ToString(),
            saga.Status);
        Assert.NotNull(saga.PaymentId);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "CapturePayment.v1",
                    cancellationToken);

        Assert.Equal(
            "payments.commands",
            outbox.Destination);
        Assert.Equal(
            messageId,
            outbox.CausationId);

        var payload =
            JsonSerializer.Deserialize<CapturePaymentV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal(saga.PaymentId, payload.PaymentId);
        Assert.Equal(35m, payload.Amount);
        Assert.Equal("USD", payload.Currency);
    }

    [Fact]
    public async Task InventoryRejectedMovesSagaAndEnqueuesCancelOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedWaitingForInventorySagaAsync(
            orderId,
            reservationId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(dbContext);

            var processor =
                new InventoryReservationRejectedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new InventoryReservationRejectedMessageHandler(
                        repository,
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedInventoryReservationRejectedMessage(
                        new InventoryReservationRejectedMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "InventoryReservationRejected.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(2),
                                "Inventory",
                                null),
                            new InventoryReservationRejectedV1(
                                orderId,
                                reservationId,
                                "INSUFFICIENT_STOCK")),
                        "inventory.events",
                        0,
                        701,
                        StartedAtUtc.AddSeconds(2)),
                    StartedAtUtc.AddSeconds(3),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var saga =
            await verificationDbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForOrderCancellation.ToString(),
            saga.Status);
        Assert.Null(saga.PaymentId);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "CancelOrder.v1",
                    cancellationToken);

        Assert.Equal(
            "orders.commands",
            outbox.Destination);
        Assert.Contains(
            "INSUFFICIENT_STOCK",
            outbox.Payload,
            StringComparison.Ordinal);
    }

    private async Task SeedWaitingForInventorySagaAsync(
        Guid orderId,
        Guid reservationId,
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
