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

public sealed class PaymentReconciliationInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 30, 19, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public async Task CapturedSnapshotContinuesCheckoutAndClearsRetryMetadata()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            scheduleRetry: true,
            cancellationToken);

        await ProcessAsync(
            orderId,
            paymentId,
            "Captured",
            "Succeeded",
            "provider-ref-reconciled",
            null,
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var saga =
            await dbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForInventoryCommit.ToString(),
            saga.Status);
        Assert.Null(saga.NextAttemptAtUtc);
        Assert.Null(saga.LastTechnicalErrorCode);
        Assert.Null(saga.LastTechnicalErrorMessage);

        var outbox =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "ConsumeInventory.v1",
                    cancellationToken);

        var payload =
            JsonSerializer.Deserialize<ConsumeInventoryV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(reservationId, payload.ReservationId);
    }

    [Fact]
    public async Task FailedSnapshotStartsInventoryCompensation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            scheduleRetry: true,
            cancellationToken);

        await ProcessAsync(
            orderId,
            paymentId,
            "Failed",
            "DefinitivelyFailed",
            null,
            "DECLINED",
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var saga =
            await dbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.CompensatingInventory.ToString(),
            saga.Status);
        Assert.Null(saga.NextAttemptAtUtc);

        var outbox =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "ReleaseInventory.v1",
                    cancellationToken);

        var payload =
            JsonSerializer.Deserialize<ReleaseInventoryV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal("DECLINED", payload.ReasonCode);
    }

    [Fact]
    public async Task RecoverableProcessingSnapshotKeepsSagaWaitingWithoutCommands()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            scheduleRetry: true,
            cancellationToken);

        await ProcessAsync(
            orderId,
            paymentId,
            "Processing",
            "Ambiguous",
            null,
            "TIMEOUT",
            cancellationToken,
            nextAttemptAtUtc:
                DeadlineAtUtc.AddMinutes(2));

        await using var dbContext =
            fixture.CreateDbContext();

        var saga =
            await dbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForPayment.ToString(),
            saga.Status);
        Assert.Equal(1, saga.RetryCount);
        Assert.False(
            await dbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    message => message.AggregateId == orderId,
                    cancellationToken));
    }

    [Fact]
    public async Task InconsistentCapturedSnapshotRequiresManualIntervention()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await SeedWaitingForPaymentSagaAsync(
            orderId,
            reservationId,
            paymentId,
            scheduleRetry: true,
            cancellationToken);

        await ProcessAsync(
            orderId,
            paymentId,
            "Captured",
            "Ambiguous",
            null,
            "TIMEOUT",
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var saga =
            await dbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.ManualInterventionRequired.ToString(),
            saga.Status);
        Assert.Equal(
            "PAYMENT_RECONCILIATION_INCONSISTENT",
            saga.LastTechnicalErrorCode);
        Assert.Null(saga.NextAttemptAtUtc);
        Assert.False(
            await dbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    message => message.AggregateId == orderId,
                    cancellationToken));
    }

    private async Task ProcessAsync(
        Guid orderId,
        Guid paymentId,
        string paymentStatus,
        string? providerOperationStatus,
        string? providerReference,
        string? lastErrorCode,
        CancellationToken cancellationToken,
        DateTimeOffset? nextAttemptAtUtc = null)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var repository =
            new CheckoutSagaRepository(
                dbContext);

        var processor =
            new PaymentReconciledInboxProcessor(
                dbContext,
                new InboxMessageRepository(
                    dbContext),
                new PaymentReconciledMessageHandler(
                    repository,
                    new SagaOutboxWriter(
                        dbContext),
                    new SagaEfUnitOfWork(
                        dbContext)));

        var occurredAtUtc =
            DeadlineAtUtc.AddMinutes(1);

        Assert.True(
            await processor.ProcessAsync(
                new ConsumedPaymentReconciledMessage(
                    new PaymentReconciledMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "PaymentReconciled.v1",
                            1,
                            orderId,
                            orderId,
                            Guid.NewGuid(),
                            occurredAtUtc,
                            "Payment",
                            null),
                        new PaymentReconciledV1(
                            orderId,
                            paymentId,
                            Guid.NewGuid(),
                            paymentStatus,
                            providerOperationStatus,
                            1,
                            nextAttemptAtUtc,
                            lastErrorCode,
                            providerReference,
                            occurredAtUtc)),
                    "payments.events",
                    0,
                    Random.Shared.NextInt64(
                        1000,
                        100000),
                    occurredAtUtc),
                occurredAtUtc.AddSeconds(1),
                cancellationToken));
    }

    private async Task SeedWaitingForPaymentSagaAsync(
        Guid orderId,
        Guid reservationId,
        Guid paymentId,
        bool scheduleRetry,
        CancellationToken cancellationToken)
    {
        var saga =
            CheckoutSaga.Start(
                orderId,
                Guid.NewGuid(),
                [
                    CheckoutSagaItem.Create(
                        "SKU-RECON-001",
                        2,
                        10m,
                        "USD"),
                    CheckoutSagaItem.Create(
                        "SKU-RECON-002",
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

        if (scheduleRetry)
        {
            saga.SchedulePaymentReconciliation(
                "PAYMENT_OUTCOME_UNKNOWN",
                "Waiting for reconciliation.",
                DeadlineAtUtc.AddSeconds(1),
                DeadlineAtUtc.AddMinutes(5));
        }

        await using var dbContext =
            fixture.CreateDbContext();

        await new CheckoutSagaRepository(
                dbContext)
            .AddAsync(
                saga,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
