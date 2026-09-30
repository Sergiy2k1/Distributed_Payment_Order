namespace PayFlow.MockPaymentProvider.Refund;

public sealed record RefundPaymentRequest(
    Guid PaymentId,
    Guid OrderId,
    Guid RefundId,
    decimal Amount,
    string Currency);
