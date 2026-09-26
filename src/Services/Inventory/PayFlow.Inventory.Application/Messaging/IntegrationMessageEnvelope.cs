namespace PayFlow.Inventory.Application.Messaging;

public sealed record IntegrationMessageEnvelope(
    Guid MessageId,
    string MessageType,
    int SchemaVersion,
    Guid AggregateId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredAtUtc,
    string Producer,
    string? TraceParent);
