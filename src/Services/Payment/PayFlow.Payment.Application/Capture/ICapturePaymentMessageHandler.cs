namespace PayFlow.Payment.Application.Capture;

public interface ICapturePaymentMessageHandler
{
    Task HandleAsync(
        CapturePaymentMessage message,
        CancellationToken cancellationToken = default);
}
