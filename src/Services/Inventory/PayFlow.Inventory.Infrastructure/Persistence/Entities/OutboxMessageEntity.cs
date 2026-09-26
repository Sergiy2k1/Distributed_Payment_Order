namespace PayFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class OutboxMessageEntity
{
    public Guid OutboxMessageId { get; set; }
    public Guid MessageId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public Guid AggregateId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string? TraceParent { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? ClaimedUntilUtc { get; set; }
}
