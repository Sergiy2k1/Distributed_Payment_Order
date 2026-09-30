using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Capture;
using PayFlow.Payment.Application.Events;
using PayFlow.Payment.Application.Messaging;
using PayFlow.Payment.Application.Reconciliation;
using PayFlow.Payment.Infrastructure.Messaging;
using PayFlow.Payment.Infrastructure.Messaging.Outbox;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Messaging;

public sealed class ReconcilePaymentInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessingPaymentPublishesCurrentReconciliationSnapshot()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();

        await SeedProcessingPaymentAsync(
            orderId,
            paymentId,
            cancellationToken);

        await using (var processDbContext =
            fixture.CreateDbContext())
        {
            var processor =
                new ReconcilePaymentInboxProcessor(
                    processDbContext,
                    new InboxMessageRepository(
                        processDbContext),
                    new ReconcilePaymentMessageHandler(
                        new PaymentRepository(
                            processDbContext),
                        new ProviderOperationRepository(
                            processDbContext),
                        new PaymentOutboxWriter(
                            processDbContext),
                        new PaymentEfUnitOfWork(
                            processDbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedReconcilePaymentMessage(
                        new ReconcilePaymentMessage(
                            new IntegrationMessageEnvelope(
                                Guid.NewGuid(),
                                "ReconcilePayment.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                OccurredAtUtc.AddSeconds(5),
                                "Saga",
                                null),
                            new ReconcilePaymentV1(
                                orderId,
                                paymentId,
                                reconciliationId)),
                        "payments.commands",
                        0,
                        700,
                        OccurredAtUtc.AddSeconds(5)),
                    OccurredAtUtc.AddSeconds(6),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                        == "PaymentReconciled.v1",
                    cancellationToken);

        var payload =
            JsonSerializer.Deserialize<PaymentReconciledV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal(paymentId, payload.PaymentId);
        Assert.Equal(
            reconciliationId,
            payload.ReconciliationId);
        Assert.Equal(
            "Processing",
            payload.PaymentStatus);
        Assert.Equal(
            "Pending",
            payload.ProviderOperationStatus);
        Assert.Equal(0, payload.ProviderAttemptCount);
        Assert.Null(payload.ProviderReference);
    }

    [Fact]
    public async Task DuplicateReconciliationMessageDoesNotCreateDuplicateSnapshot()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedProcessingPaymentAsync(
            orderId,
            paymentId,
            cancellationToken);

        Assert.True(
            await ProcessReconciliationAsync(
                orderId,
                paymentId,
                reconciliationId,
                messageId,
                710,
                cancellationToken));

        Assert.False(
            await ProcessReconciliationAsync(
                orderId,
                paymentId,
                reconciliationId,
                messageId,
                711,
                cancellationToken));

        await using var verificationDbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                        == "PaymentReconciled.v1",
                    cancellationToken));
    }

    private async Task SeedProcessingPaymentAsync(
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var handler =
            new CapturePaymentMessageHandler(
                new PaymentRepository(dbContext),
                new ProviderOperationRepository(dbContext),
                new PaymentEfUnitOfWork(dbContext));

        await handler.HandleAsync(
            new CapturePaymentMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "CapturePayment.v1",
                    1,
                    orderId,
                    orderId,
                    null,
                    OccurredAtUtc,
                    "Saga",
                    null),
                new CapturePaymentV1(
                    orderId,
                    paymentId,
                    35m,
                    "USD")),
            cancellationToken);
    }

    private async Task<bool> ProcessReconciliationAsync(
        Guid orderId,
        Guid paymentId,
        Guid reconciliationId,
        Guid messageId,
        long sourceOffset,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var processor =
            new ReconcilePaymentInboxProcessor(
                dbContext,
                new InboxMessageRepository(
                    dbContext),
                new ReconcilePaymentMessageHandler(
                    new PaymentRepository(
                        dbContext),
                    new ProviderOperationRepository(
                        dbContext),
                    new PaymentOutboxWriter(
                        dbContext),
                    new PaymentEfUnitOfWork(
                        dbContext)));

        return await processor.ProcessAsync(
            new ConsumedReconcilePaymentMessage(
                new ReconcilePaymentMessage(
                    new IntegrationMessageEnvelope(
                        messageId,
                        "ReconcilePayment.v1",
                        1,
                        orderId,
                        orderId,
                        null,
                        OccurredAtUtc.AddSeconds(5),
                        "Saga",
                        null),
                    new ReconcilePaymentV1(
                        orderId,
                        paymentId,
                        reconciliationId)),
                "payments.commands",
                0,
                sourceOffset,
                OccurredAtUtc.AddSeconds(5)),
            OccurredAtUtc.AddSeconds(6),
            cancellationToken);
    }
}
