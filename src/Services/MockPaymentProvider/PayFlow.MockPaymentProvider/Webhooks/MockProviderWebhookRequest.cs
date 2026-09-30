namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed record MockProviderWebhookRequest(
    Guid EventId,
    string EventType,
    string OperationType,
    Guid PaymentId,
    Guid? RefundId,
    string Outcome,
    string? ProviderReference,
    string? ErrorCode,
    DateTimeOffset OccurredAtUtc);
