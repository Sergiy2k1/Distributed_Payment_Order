namespace PayFlow.Saga.Infrastructure.Persistence.Entities;

public sealed class InboxMessageEntity
{
    public string ConsumerName { get; set; } = string.Empty;

    public Guid MessageId { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    public Guid AggregateId { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? CausationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string SourceTopic { get; set; } = string.Empty;

    public int SourcePartition { get; set; }

    public long SourceOffset { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
