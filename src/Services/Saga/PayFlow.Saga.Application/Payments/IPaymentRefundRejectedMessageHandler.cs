namespace PayFlow.Saga.Application.Payments;

public interface IPaymentRefundRejectedMessageHandler
{
    Task HandleAsync(
        PaymentRefundRejectedMessage message,
        CancellationToken cancellationToken = default);
}
