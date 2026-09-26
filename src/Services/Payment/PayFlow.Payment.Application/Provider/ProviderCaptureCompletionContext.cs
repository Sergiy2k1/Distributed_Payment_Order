namespace PayFlow.Payment.Application.Provider;

public sealed record ProviderCaptureCompletionContext(
    Guid PaymentId,
    Guid OrderId,
    Guid CorrelationId,
    Guid? CausationId,
    string? TraceParent);
