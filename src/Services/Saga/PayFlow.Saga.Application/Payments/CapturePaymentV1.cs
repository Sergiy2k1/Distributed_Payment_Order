namespace PayFlow.Saga.Application.Payments;

public sealed record CapturePaymentV1(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Currency);
