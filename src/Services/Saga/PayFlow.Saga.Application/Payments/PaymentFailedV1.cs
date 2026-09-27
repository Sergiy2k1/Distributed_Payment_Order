namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentFailedV1(
    Guid OrderId,
    Guid PaymentId,
    string ReasonCode);
