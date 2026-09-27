namespace PayFlow.Payment.Application.Provider;

public sealed record ProviderRefundCompletionContext(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    Guid CorrelationId,
    Guid? CausationId,
    string? TraceParent);
