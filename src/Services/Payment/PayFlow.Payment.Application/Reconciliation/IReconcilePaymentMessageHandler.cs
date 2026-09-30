namespace PayFlow.Payment.Application.Reconciliation;

public interface IReconcilePaymentMessageHandler
{
    Task HandleAsync(
        ReconcilePaymentMessage message,
        CancellationToken cancellationToken = default);
}
