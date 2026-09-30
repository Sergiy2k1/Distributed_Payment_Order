namespace PayFlow.Saga.Application.Payments;

public sealed record ReconcilePaymentV1(
    Guid OrderId,
    Guid PaymentId,
    Guid ReconciliationId);
