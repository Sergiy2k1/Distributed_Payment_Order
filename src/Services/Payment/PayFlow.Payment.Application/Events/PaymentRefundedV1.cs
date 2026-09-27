namespace PayFlow.Payment.Application.Events;

public sealed record PaymentRefundedV1(
    Guid OrderId,
    Guid PaymentId,
    Guid RefundId,
    decimal Amount,
    string Currency,
    DateTimeOffset RefundedAtUtc);
