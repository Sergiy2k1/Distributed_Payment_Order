namespace PayFlow.Payment.Application.Provider;

public sealed record PaymentProviderRefundRequest(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);
