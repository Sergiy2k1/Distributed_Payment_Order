namespace PayFlow.Payment.Application.Provider;

public sealed record PaymentProviderCaptureRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);
