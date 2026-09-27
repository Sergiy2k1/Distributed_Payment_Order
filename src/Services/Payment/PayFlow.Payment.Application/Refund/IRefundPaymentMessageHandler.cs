namespace PayFlow.Payment.Application.Refund;

public interface IRefundPaymentMessageHandler
{
    Task HandleAsync(
        RefundPaymentMessage message,
        CancellationToken cancellationToken = default);
}
