namespace PayFlow.Payment.Application.Reconciliation;

public sealed record ReconcilePaymentV1(
    Guid OrderId,
    Guid PaymentId,
    Guid ReconciliationId);
