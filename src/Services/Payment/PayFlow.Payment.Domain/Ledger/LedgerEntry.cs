namespace PayFlow.Payment.Domain.Ledger;

public sealed record LedgerEntry
{
    private LedgerEntry(
        Guid ledgerEntryId,
        string accountId,
        LedgerEntrySide side,
        decimal amount,
        string currency)
    {
        LedgerEntryId = ledgerEntryId;
        AccountId = accountId;
        Side = side;
        Amount = amount;
        Currency = currency;
    }

    public Guid LedgerEntryId { get; }
    public string AccountId { get; }
    public LedgerEntrySide Side { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    public static LedgerEntry Create(
        Guid ledgerEntryId,
        string accountId,
        LedgerEntrySide side,
        decimal amount,
        string currency)
    {
        if (ledgerEntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger entry ID cannot be empty.",
                nameof(ledgerEntryId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Ledger entry amount must be greater than zero.");
        }

        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException(
                "Currency must be a 3-letter code.",
                nameof(currency));
        }

        return new LedgerEntry(
            ledgerEntryId,
            accountId.Trim(),
            side,
            amount,
            normalizedCurrency);
    }
}
