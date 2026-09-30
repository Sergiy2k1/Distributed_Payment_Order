namespace PayFlow.Payment.Api.Webhooks;

public sealed record ProviderWebhookRequest(
    Guid EventId,
    string EventType,
    string OperationType,
    Guid PaymentId,
    Guid? RefundId,
    string Outcome,
    string? ProviderReference,
    string? ErrorCode,
    DateTimeOffset OccurredAtUtc);
