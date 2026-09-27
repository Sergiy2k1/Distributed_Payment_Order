namespace PayFlow.Payment.Application.Provider;

public interface IPaymentProvider
{
    Task<PaymentProviderCaptureResult> CaptureAsync(
        PaymentProviderCaptureRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken = default);
}
