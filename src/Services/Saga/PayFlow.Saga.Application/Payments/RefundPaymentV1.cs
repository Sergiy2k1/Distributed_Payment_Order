namespace PayFlow.Saga.Application.Payments;

public sealed record RefundPaymentV1(
    Guid OrderId,
    Guid PaymentId,
    Guid RefundId,
    decimal Amount,
    string Currency,
    string ReasonCode);
