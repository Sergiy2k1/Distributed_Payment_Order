namespace PayFlow.Payment.Application.Events;

public sealed record PaymentRefundRejectedV1(
    Guid OrderId,
    Guid PaymentId,
    Guid RefundId,
    string ReasonCode);
