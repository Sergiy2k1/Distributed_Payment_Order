namespace PayFlow.Saga.Application.Payments;

public interface IPaymentRefundedMessageHandler
{
    Task HandleAsync(
        PaymentRefundedMessage message,
        CancellationToken cancellationToken = default);
}
