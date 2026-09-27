namespace PayFlow.Payment.Application.Provider;

public sealed record ProviderRefundWorkItem(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string ProviderIdempotencyKey,
    int AttemptCount,
    Guid? CorrelationId,
    Guid? CausationId,
    string? TraceParent);
