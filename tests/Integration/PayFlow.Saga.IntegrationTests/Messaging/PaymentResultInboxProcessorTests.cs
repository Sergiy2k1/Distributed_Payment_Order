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

public sealed class PaymentResultInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 27, 18, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public async Task PaymentCapturedMovesSagaAndEnqueuesConsumeInventory()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(dbContext);

            var processor =
                new PaymentCapturedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new PaymentCapturedMessageHandler(
                        repository,
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedPaymentCapturedMessage(
                        new PaymentCapturedMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "PaymentCaptured.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(3),
                                "Payment",
                                null),
                            new PaymentCapturedV1(
                                orderId,
                                paymentId,
                                35m,
                                "USD",
                                StartedAtUtc.AddSeconds(3),
                                "provider-ref-001")),
                        "payments.events",
                        0,
                        800,
                        StartedAtUtc.AddSeconds(3)),
                    StartedAtUtc.AddSeconds(4),
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
            CheckoutSagaStatus.WaitingForInventoryCommit.ToString(),
            saga.Status);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "ConsumeInventory.v1",
                    cancellationToken);

        Assert.Equal(
            "inventory.commands",
            outbox.Destination);
        Assert.Equal(
            messageId,
            outbox.CausationId);

        var payload =
            JsonSerializer.Deserialize<ConsumeInventoryV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal(reservationId, payload.ReservationId);
    }

    [Fact]
    public async Task PaymentFailedMovesSagaAndEnqueuesReleaseInventory()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(dbContext);

            var processor =
                new PaymentFailedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new PaymentFailedMessageHandler(
                        repository,
                        new SagaOutboxWriter(dbContext),
                        new SagaEfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedPaymentFailedMessage(
                        new PaymentFailedMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "PaymentFailed.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                StartedAtUtc.AddSeconds(3),
                                "Payment",
                                null),
                            new PaymentFailedV1(
                                orderId,
                                paymentId,
                                "DECLINED")),
                        "payments.events",
                        0,
                        801,
                        StartedAtUtc.AddSeconds(3)),
                    StartedAtUtc.AddSeconds(4),
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
            CheckoutSagaStatus.CompensatingInventory.ToString(),
            saga.Status);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "ReleaseInventory.v1",
                    cancellationToken);

        Assert.Equal(
            "inventory.commands",
            outbox.Destination);

        var payload =
            JsonSerializer.Deserialize<ReleaseInventoryV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal(reservationId, payload.ReservationId);
        Assert.Equal("DECLINED", payload.ReasonCode);
    }

    private async Task SeedWaitingForPaymentSagaAsync(
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
