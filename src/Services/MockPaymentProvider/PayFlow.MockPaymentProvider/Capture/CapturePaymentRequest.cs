namespace PayFlow.MockPaymentProvider.Capture;

public sealed record CapturePaymentRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency);
