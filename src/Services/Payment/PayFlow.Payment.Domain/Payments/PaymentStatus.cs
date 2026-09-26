namespace PayFlow.Payment.Domain.Payments;

public enum PaymentStatus
{
    Created = 0,
    Processing = 1,
    Captured = 2,
    Failed = 3,
    RefundPending = 4,
    Refunded = 5
}
