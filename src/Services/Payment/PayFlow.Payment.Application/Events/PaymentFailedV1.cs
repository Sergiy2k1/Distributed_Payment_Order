namespace PayFlow.Payment.Application.Events;

public sealed record PaymentFailedV1(
    Guid OrderId,
    Guid PaymentId,
    string ReasonCode);
