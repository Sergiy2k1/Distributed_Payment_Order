using System.Collections.ObjectModel;

namespace PayFlow.Payment.Domain.Ledger;

public sealed class LedgerTransaction
{
    private LedgerTransaction(
        Guid ledgerTransactionId,
        string operationType,
        Guid businessOperationId,
        string currency,
        DateTimeOffset occurredAtUtc,
        ReadOnlyCollection<LedgerEntry> entries)
    {
        LedgerTransactionId = ledgerTransactionId;
        OperationType = operationType;
        BusinessOperationId = businessOperationId;
        Currency = currency;
        OccurredAtUtc = occurredAtUtc;
        Entries = entries;
    }

    public Guid LedgerTransactionId { get; }
    public string OperationType { get; }
    public Guid BusinessOperationId { get; }
    public string Currency { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public IReadOnlyList<LedgerEntry> Entries { get; }

    public decimal DebitTotal =>
        Entries
            .Where(entry => entry.Side == LedgerEntrySide.Debit)
            .Sum(entry => entry.Amount);

    public decimal CreditTotal =>
        Entries
            .Where(entry => entry.Side == LedgerEntrySide.Credit)
            .Sum(entry => entry.Amount);

    public static LedgerTransaction Create(
        Guid ledgerTransactionId,
        string operationType,
        Guid businessOperationId,
        string currency,
        DateTimeOffset occurredAtUtc,
        IEnumerable<LedgerEntry> entries)
    {
        if (ledgerTransactionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger transaction ID cannot be empty.",
                nameof(ledgerTransactionId));
        }

        if (businessOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Business operation ID cannot be empty.",
                nameof(businessOperationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(entries);

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Ledger timestamp must use UTC offset.",
                nameof(occurredAtUtc));
        }

        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException(
                "Currency must be a 3-letter code.",
                nameof(currency));
        }

        var entryArray = entries.ToArray();

        if (entryArray.Length < 2)
        {
            throw new ArgumentException(
                "Double-entry transaction requires at least two entries.",
                nameof(entries));
        }

        if (entryArray.Any(
                entry => !string.Equals(
                    entry.Currency,
                    normalizedCurrency,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "All ledger entries must use the transaction currency.",
                nameof(entries));
        }

        var debitTotal = entryArray
            .Where(entry => entry.Side == LedgerEntrySide.Debit)
            .Sum(entry => entry.Amount);

        var creditTotal = entryArray
            .Where(entry => entry.Side == LedgerEntrySide.Credit)
            .Sum(entry => entry.Amount);

        if (debitTotal != creditTotal)
        {
            throw new InvalidOperationException(
                "Ledger transaction must balance exactly.");
        }

        return new LedgerTransaction(
            ledgerTransactionId,
            operationType.Trim(),
            businessOperationId,
            normalizedCurrency,
            occurredAtUtc,
            Array.AsReadOnly(entryArray));
    }

    public static LedgerTransaction CreateCapture(
        Guid ledgerTransactionId,
        Guid paymentId,
        decimal amount,
        string currency,
        DateTimeOffset occurredAtUtc)
    {
        return Create(
            ledgerTransactionId,
            "Capture",
            paymentId,
            currency,
            occurredAtUtc,
            [
                LedgerEntry.Create(
                    Guid.NewGuid(),
                    "provider-clearing-asset",
                    LedgerEntrySide.Debit,
                    amount,
                    currency),
                LedgerEntry.Create(
                    Guid.NewGuid(),
                    "merchant-settlement-payable",
                    LedgerEntrySide.Credit,
                    amount,
                    currency)
            ]);
    }
}
