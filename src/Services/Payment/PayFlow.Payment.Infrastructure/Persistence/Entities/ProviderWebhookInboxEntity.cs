namespace PayFlow.Payment.Infrastructure.Persistence.Entities;

public sealed class ProviderWebhookInboxEntity
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public Guid? RefundId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
}
