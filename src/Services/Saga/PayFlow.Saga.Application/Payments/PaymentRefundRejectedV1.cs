namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentRefundRejectedV1(
    Guid OrderId,
    Guid PaymentId,
    Guid RefundId,
    string ReasonCode);
