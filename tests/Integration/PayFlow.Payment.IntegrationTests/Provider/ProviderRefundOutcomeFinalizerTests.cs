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

public sealed class ProviderRefundOutcomeFinalizerTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessMarksRefundedCreatesReversingLedgerAndOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        await SeedRefundAttemptAsync(
            paymentId,
            orderId,
            refundId,
            cancellationToken);

        await using (var dbContext = fixture.CreateDbContext())
        {
            await CreateFinalizer(dbContext).FinalizeAsync(
                new ProviderRefundCompletionContext(
                    refundId,
                    paymentId,
                    orderId,
                    orderId,
                    null,
                    null),
                PaymentProviderRefundResult.Succeeded("refund-ref-001"),
                CreatedAtUtc.AddSeconds(4),
                CreatedAtUtc.AddMinutes(1),
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var payment =
            await verificationDbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    x => x.PaymentId == paymentId,
                    cancellationToken);

        Assert.Equal(
            PaymentStatus.Refunded.ToString(),
            payment.Status);

        var refundLedger =
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .SingleAsync(
                    x => x.OperationType == "Refund"
                         && x.BusinessOperationId == refundId,
                    cancellationToken);

        var entries =
            await verificationDbContext.LedgerEntries
                .AsNoTracking()
                .Where(x =>
                    x.LedgerTransactionId
                    == refundLedger.LedgerTransactionId)
                .ToArrayAsync(cancellationToken);

        Assert.Equal(
            entries.Where(x => x.Side == "Debit").Sum(x => x.Amount),
            entries.Where(x => x.Side == "Credit").Sum(x => x.Amount));

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    x => x.AggregateId == orderId
                         && x.MessageType == "PaymentRefunded.v1",
                    cancellationToken));
    }

    [Fact]
    public async Task AmbiguousRefundDoesNotCreateLedgerOrTerminalOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        await SeedRefundAttemptAsync(
            paymentId,
            orderId,
            refundId,
            cancellationToken);

        await using (var dbContext = fixture.CreateDbContext())
        {
            await CreateFinalizer(dbContext).FinalizeAsync(
                new ProviderRefundCompletionContext(
                    refundId,
                    paymentId,
                    orderId,
                    orderId,
                    null,
                    null),
                PaymentProviderRefundResult.Ambiguous("TIMEOUT"),
                CreatedAtUtc.AddSeconds(4),
                CreatedAtUtc.AddMinutes(1),
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        Assert.False(
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .AnyAsync(
                    x => x.OperationType == "Refund"
                         && x.BusinessOperationId == refundId,
                    cancellationToken));

        Assert.False(
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    x => x.AggregateId == orderId,
                    cancellationToken));
    }

    private static ProviderRefundOutcomeFinalizer CreateFinalizer(
        PaymentDbContext dbContext)
    {
        return new ProviderRefundOutcomeFinalizer(
            new PaymentRepository(dbContext),
            new ProviderOperationRepository(dbContext),
            new LedgerRepository(dbContext),
            new PaymentOutboxWriter(dbContext),
            new PaymentEfUnitOfWork(dbContext));
    }

    private async Task SeedRefundAttemptAsync(
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

        payment.StartProcessing(CreatedAtUtc.AddSeconds(1));
        payment.MarkCaptured(CreatedAtUtc.AddSeconds(2));
        payment.StartRefund(CreatedAtUtc.AddSeconds(3));

        var operation =
            ProviderOperation.CreateRefund(
                refundId,
                paymentId,
                CreatedAtUtc.AddSeconds(3),
                orderId,
                Guid.NewGuid(),
                null);

        operation.BeginAttempt(CreatedAtUtc.AddSeconds(3));

        await using var dbContext = fixture.CreateDbContext();

        await new PaymentRepository(dbContext)
            .AddAsync(payment, cancellationToken);
        await new ProviderOperationRepository(dbContext)
            .AddAsync(operation, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
