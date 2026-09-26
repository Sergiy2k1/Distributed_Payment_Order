namespace PayFlow.Payment.Application.Provider;

public sealed record ProviderCaptureWorkItem(
    Guid ProviderOperationId,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string ProviderIdempotencyKey,
    int AttemptCount);
