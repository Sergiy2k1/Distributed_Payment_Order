using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Messaging.Outbox;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.IntegrationTests.Provider;

public sealed class ProviderCaptureOutcomeFinalizerTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 0, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessPersistsCapturedPaymentBalancedLedgerAndOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await SeedProcessingAttemptAsync(
            paymentId,
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var finalizer =
                CreateFinalizer(dbContext);

            await finalizer.FinalizeAsync(
                new ProviderCaptureCompletionContext(
                    paymentId,
                    orderId,
                    orderId,
                    null,
                    null),
                PaymentProviderCaptureResult.Succeeded(
                    "provider-ref-001"),
                CreatedAtUtc.AddSeconds(2),
                CreatedAtUtc.AddMinutes(1),
                cancellationToken);
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
                        entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Succeeded.ToString(),
            operation.Status);

        var transaction =
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken);

        var entries =
            await verificationDbContext.LedgerEntries
                .AsNoTracking()
                .Where(
                    entry =>
                        entry.LedgerTransactionId
                        == transaction.LedgerTransactionId)
                .ToArrayAsync(cancellationToken);

        Assert.Equal(2, entries.Length);
        Assert.Equal(
            entries
                .Where(entry => entry.Side == "Debit")
                .Sum(entry => entry.Amount),
            entries
                .Where(entry => entry.Side == "Credit")
                .Sum(entry => entry.Amount));

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType == "PaymentCaptured.v1",
                    cancellationToken));
    }

    [Fact]
    public async Task AmbiguousOutcomeDoesNotFailPaymentOrCreateLedgerOrOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await SeedProcessingAttemptAsync(
            paymentId,
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            await CreateFinalizer(dbContext)
                .FinalizeAsync(
                    new ProviderCaptureCompletionContext(
                        paymentId,
                        orderId,
                        orderId,
                        null,
                        null),
                    PaymentProviderCaptureResult.Ambiguous(
                        "TIMEOUT"),
                    CreatedAtUtc.AddSeconds(2),
                    CreatedAtUtc.AddMinutes(1),
                    cancellationToken);
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
            PaymentStatus.Processing.ToString(),
            payment.Status);

        var operation =
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Ambiguous.ToString(),
            operation.Status);
        Assert.Equal("TIMEOUT", operation.LastErrorCode);

        Assert.False(
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken));

        Assert.False(
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    entity => entity.AggregateId == orderId,
                    cancellationToken));
    }

    [Fact]
    public async Task DefinitiveFailurePersistsFailedPaymentAndOutboxWithoutLedger()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await SeedProcessingAttemptAsync(
            paymentId,
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            await CreateFinalizer(dbContext)
                .FinalizeAsync(
                    new ProviderCaptureCompletionContext(
                        paymentId,
                        orderId,
                        orderId,
                        null,
                        null),
                    PaymentProviderCaptureResult.DefinitivelyFailed(
                        "DECLINED"),
                    CreatedAtUtc.AddSeconds(2),
                    CreatedAtUtc.AddMinutes(1),
                    cancellationToken);
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
            PaymentStatus.Failed.ToString(),
            payment.Status);
        Assert.Equal("DECLINED", payment.FailureReasonCode);

        Assert.False(
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken));

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.AggregateId == orderId
                        && entity.MessageType == "PaymentFailed.v1",
                    cancellationToken));
    }

    private static ProviderCaptureOutcomeFinalizer CreateFinalizer(
        PaymentDbContext dbContext)
    {
        return new ProviderCaptureOutcomeFinalizer(
            new PaymentRepository(dbContext),
            new ProviderOperationRepository(dbContext),
            new LedgerRepository(dbContext),
            new PaymentOutboxWriter(dbContext),
            new PaymentEfUnitOfWork(dbContext));
    }

    private async Task SeedProcessingAttemptAsync(
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

        payment.StartProcessing(CreatedAtUtc);

        var operation =
            ProviderOperation.CreateCapture(
                Guid.NewGuid(),
                paymentId,
                CreatedAtUtc);

        operation.BeginAttempt(
            CreatedAtUtc.AddSeconds(1));

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
}
