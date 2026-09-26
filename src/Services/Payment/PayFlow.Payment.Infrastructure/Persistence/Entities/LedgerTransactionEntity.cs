namespace PayFlow.Payment.Infrastructure.Persistence.Entities;

public sealed class LedgerTransactionEntity
{
    public Guid LedgerTransactionId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public Guid BusinessOperationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public List<LedgerEntryEntity> Entries { get; set; } = [];
}
