namespace PayFlow.Saga.Application.Payments;

public interface IPaymentCapturedMessageHandler
{
    Task HandleAsync(
        PaymentCapturedMessage message,
        CancellationToken cancellationToken = default);
}
