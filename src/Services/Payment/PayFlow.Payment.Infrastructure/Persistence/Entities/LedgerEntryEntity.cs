namespace PayFlow.Payment.Infrastructure.Persistence.Entities;

public sealed class LedgerEntryEntity
{
    public Guid LedgerEntryId { get; set; }
    public Guid LedgerTransactionId { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public LedgerTransactionEntity Transaction { get; set; } = null!;
}
