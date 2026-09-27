namespace PayFlow.Saga.Application.Payments;

public interface IPaymentFailedMessageHandler
{
    Task HandleAsync(
        PaymentFailedMessage message,
        CancellationToken cancellationToken = default);
}
