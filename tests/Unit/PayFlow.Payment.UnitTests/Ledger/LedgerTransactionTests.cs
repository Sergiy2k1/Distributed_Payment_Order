using PayFlow.Payment.Domain.Ledger;

namespace PayFlow.Payment.UnitTests.Ledger;

public sealed class LedgerTransactionTests
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void CapturePostingBalancesExactly()
    {
        var transaction =
            LedgerTransaction.CreateCapture(
                Guid.NewGuid(),
                Guid.NewGuid(),
                35.25m,
                "USD",
                OccurredAtUtc);

        Assert.Equal(
            transaction.DebitTotal,
            transaction.CreditTotal);
        Assert.Equal(35.25m, transaction.DebitTotal);
        Assert.Equal(2, transaction.Entries.Count);
    }

    [Fact]
    public void CreateRejectsUnbalancedTransaction()
    {
        var entries = new[]
        {
            LedgerEntry.Create(
                Guid.NewGuid(),
                "provider-clearing-asset",
                LedgerEntrySide.Debit,
                35m,
                "USD"),
            LedgerEntry.Create(
                Guid.NewGuid(),
                "merchant-settlement-payable",
                LedgerEntrySide.Credit,
                34m,
                "USD")
        };

        Assert.Throws<InvalidOperationException>(
            () => LedgerTransaction.Create(
                Guid.NewGuid(),
                "Capture",
                Guid.NewGuid(),
                "USD",
                OccurredAtUtc,
                entries));
    }

    [Fact]
    public void CreateRejectsMixedCurrencies()
    {
        var entries = new[]
        {
            LedgerEntry.Create(
                Guid.NewGuid(),
                "provider-clearing-asset",
                LedgerEntrySide.Debit,
                35m,
                "USD"),
            LedgerEntry.Create(
                Guid.NewGuid(),
                "merchant-settlement-payable",
                LedgerEntrySide.Credit,
                35m,
                "EUR")
        };

        Assert.Throws<ArgumentException>(
            () => LedgerTransaction.Create(
                Guid.NewGuid(),
                "Capture",
                Guid.NewGuid(),
                "USD",
                OccurredAtUtc,
                entries));
    }
}
