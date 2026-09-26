using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Domain.Ledger;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Persistence;

public sealed class PaymentPersistenceTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task PaymentProviderOperationAndLedgerPersistTogether()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var now =
            new DateTimeOffset(
                2026,
                9,
                26,
                23,
                45,
                0,
                TimeSpan.Zero);

        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35m,
            "USD",
            now);

        var operation =
            ProviderOperation.CreateCapture(
                Guid.NewGuid(),
                payment.PaymentId,
                now);

        var ledger =
            LedgerTransaction.CreateCapture(
                Guid.NewGuid(),
                payment.PaymentId,
                payment.Amount,
                payment.Currency,
                now);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            await new PaymentRepository(dbContext)
                .AddAsync(payment, cancellationToken);

            await new ProviderOperationRepository(dbContext)
                .AddAsync(operation, cancellationToken);

            await new LedgerRepository(dbContext)
                .AddAsync(ledger, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            1,
            await verificationDbContext.Payments
                .AsNoTracking()
                .CountAsync(cancellationToken));

        Assert.Equal(
            1,
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .CountAsync(cancellationToken));

        Assert.Equal(
            1,
            await verificationDbContext.LedgerTransactions
                .AsNoTracking()
                .CountAsync(cancellationToken));

        Assert.Equal(
            2,
            await verificationDbContext.LedgerEntries
                .AsNoTracking()
                .CountAsync(cancellationToken));
    }
}
