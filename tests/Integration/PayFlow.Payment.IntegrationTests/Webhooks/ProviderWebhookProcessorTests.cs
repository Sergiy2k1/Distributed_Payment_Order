using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Messaging.Outbox;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Entities;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.IntegrationTests.Webhooks;

public sealed class ProviderWebhookProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 30, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task DelayedCaptureSuccessFinalizesPaymentLedgerOutboxAndWebhook()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await SeedAmbiguousCaptureAsync(
            paymentId,
            orderId,
            cancellationToken);

        await InsertWebhookAsync(
            new ProviderWebhookInboxEntity
            {
                EventId = eventId,
                EventType = "PaymentProviderResult.v1",
                OperationType = "Capture",
                PaymentId = paymentId,
                RefundId = null,
                Outcome = "Succeeded",
                ProviderReference = "provider-capture-delayed",
                ErrorCode = null,
                OccurredAtUtc = CreatedAtUtc.AddSeconds(2),
                ReceivedAtUtc = CreatedAtUtc.AddMinutes(2),
                PayloadHash = Hash('A')
            },
            cancellationToken);

        await using (var processDbContext =
            fixture.CreateDbContext())
        {
            var processor =
                CreateProcessor(
                    processDbContext);

            Assert.True(
                await processor.ProcessAsync(
                    eventId,
                    CreatedAtUtc.AddMinutes(2).AddSeconds(1),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var payment =
            await verificationDbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.PaymentId == paymentId,
                    cancellationToken);

        Assert.Equal(
            PaymentStatus.Captured.ToString(),
            payment.Status);

        var operation =
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.OperationType == "Capture"
                        && entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Succeeded.ToString(),
            operation.Status);
        Assert.Equal(
            "provider-capture-delayed",
            operation.ProviderReference);

        Assert.Equal(
            1,
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.OperationType == "Capture"
                        && entity.BusinessOperationId == paymentId,
                    cancellationToken));

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType == "PaymentCaptured.v1",
                    cancellationToken));

        var webhook =
            await verificationDbContext.ProviderWebhookInbox
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.EventId == eventId,
                    cancellationToken);

        Assert.NotNull(webhook.ProcessedAtUtc);
    }

    [Fact]
    public async Task DelayedRefundSuccessFinalizesRefundLedgerOutboxAndWebhook()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await SeedAmbiguousRefundAsync(
            paymentId,
            orderId,
            refundId,
            cancellationToken);

        await InsertWebhookAsync(
            new ProviderWebhookInboxEntity
            {
                EventId = eventId,
                EventType = "PaymentProviderResult.v1",
                OperationType = "Refund",
                PaymentId = paymentId,
                RefundId = refundId,
                Outcome = "Succeeded",
                ProviderReference = "provider-refund-delayed",
                ErrorCode = null,
                OccurredAtUtc = CreatedAtUtc.AddSeconds(5),
                ReceivedAtUtc = CreatedAtUtc.AddMinutes(2),
                PayloadHash = Hash('B')
            },
            cancellationToken);

        await using (var processDbContext =
            fixture.CreateDbContext())
        {
            var processor =
                CreateProcessor(
                    processDbContext);

            Assert.True(
                await processor.ProcessAsync(
                    eventId,
                    CreatedAtUtc.AddMinutes(2).AddSeconds(1),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var payment =
            await verificationDbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.PaymentId == paymentId,
                    cancellationToken);

        Assert.Equal(
            PaymentStatus.Refunded.ToString(),
            payment.Status);

        var operation =
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.OperationType == "Refund"
                        && entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Succeeded.ToString(),
            operation.Status);

        Assert.Equal(
            1,
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.OperationType == "Refund"
                        && entity.BusinessOperationId == refundId,
                    cancellationToken));

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType == "PaymentRefunded.v1",
                    cancellationToken));

        var webhook =
            await verificationDbContext.ProviderWebhookInbox
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.EventId == eventId,
                    cancellationToken);

        Assert.NotNull(webhook.ProcessedAtUtc);
    }

    private static ProviderWebhookProcessor CreateProcessor(
        PaymentDbContext dbContext)
    {
        var paymentRepository =
            new PaymentRepository(dbContext);
        var operationRepository =
            new ProviderOperationRepository(dbContext);
        var ledgerRepository =
            new LedgerRepository(dbContext);
        var outboxWriter =
            new PaymentOutboxWriter(dbContext);
        var unitOfWork =
            new PaymentEfUnitOfWork(dbContext);

        return new ProviderWebhookProcessor(
            dbContext,
            new ProviderWebhookInboxRepository(dbContext),
            new ProviderCaptureOutcomeFinalizer(
                paymentRepository,
                operationRepository,
                ledgerRepository,
                outboxWriter,
                unitOfWork),
            new ProviderRefundOutcomeFinalizer(
                paymentRepository,
                operationRepository,
                ledgerRepository,
                outboxWriter,
                unitOfWork));
    }

    private async Task SeedAmbiguousCaptureAsync(
        Guid paymentId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var payment =
            PaymentAggregate.Create(
                paymentId,
                orderId,
                35m,
                "USD",
                CreatedAtUtc);

        payment.StartProcessing(
            CreatedAtUtc.AddSeconds(1));

        var operation =
            ProviderOperation.CreateCapture(
                Guid.NewGuid(),
                paymentId,
                CreatedAtUtc,
                orderId,
                Guid.NewGuid(),
                null);

        operation.BeginAttempt(
            CreatedAtUtc.AddSeconds(1));

        operation.MarkAmbiguous(
            "TIMEOUT",
            CreatedAtUtc.AddSeconds(3),
            CreatedAtUtc.AddMinutes(1));

        await using var dbContext =
            fixture.CreateDbContext();

        await new PaymentRepository(dbContext)
            .AddAsync(
                payment,
                cancellationToken);

        await new ProviderOperationRepository(dbContext)
            .AddAsync(
                operation,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task SeedAmbiguousRefundAsync(
        Guid paymentId,
        Guid orderId,
        Guid refundId,
        CancellationToken cancellationToken)
    {
        var payment =
            PaymentAggregate.Create(
                paymentId,
                orderId,
                35m,
                "USD",
                CreatedAtUtc);

        payment.StartProcessing(
            CreatedAtUtc.AddSeconds(1));
        payment.MarkCaptured(
            CreatedAtUtc.AddSeconds(2));
        payment.StartRefund(
            CreatedAtUtc.AddSeconds(3));

        var operation =
            ProviderOperation.CreateRefund(
                refundId,
                paymentId,
                CreatedAtUtc.AddSeconds(3),
                orderId,
                Guid.NewGuid(),
                null);

        operation.BeginAttempt(
            CreatedAtUtc.AddSeconds(3));

        operation.MarkAmbiguous(
            "TIMEOUT",
            CreatedAtUtc.AddSeconds(6),
            CreatedAtUtc.AddMinutes(1));

        await using var dbContext =
            fixture.CreateDbContext();

        await new PaymentRepository(dbContext)
            .AddAsync(
                payment,
                cancellationToken);

        await new ProviderOperationRepository(dbContext)
            .AddAsync(
                operation,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task InsertWebhookAsync(
        ProviderWebhookInboxEntity webhook,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            ProviderWebhookInsertResult.Inserted,
            await new ProviderWebhookInboxRepository(
                    dbContext)
                .TryInsertAsync(
                    webhook,
                    cancellationToken));
    }

    private static string Hash(
        char value)
    {
        return new string(
            value,
            64);
    }
}
