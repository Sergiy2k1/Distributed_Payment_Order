namespace PayFlow.Payment.Application.Capture;

public sealed record CapturePaymentV1(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Currency);
