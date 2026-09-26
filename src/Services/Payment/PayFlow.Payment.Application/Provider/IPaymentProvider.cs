namespace PayFlow.Payment.Application.Provider;

public interface IPaymentProvider
{
    Task<PaymentProviderCaptureResult> CaptureAsync(
        PaymentProviderCaptureRequest request,
        CancellationToken cancellationToken = default);
}
