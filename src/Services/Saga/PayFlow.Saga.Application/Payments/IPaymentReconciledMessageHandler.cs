namespace PayFlow.Saga.Application.Payments;

public interface IPaymentReconciledMessageHandler
{
    Task HandleAsync(
        PaymentReconciledMessage message,
        CancellationToken cancellationToken = default);
}
