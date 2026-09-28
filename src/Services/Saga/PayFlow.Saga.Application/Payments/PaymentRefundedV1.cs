namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentRefundedV1(
    Guid OrderId,
    Guid PaymentId,
    Guid RefundId,
    decimal Amount,
    string Currency,
    DateTimeOffset RefundedAtUtc);
