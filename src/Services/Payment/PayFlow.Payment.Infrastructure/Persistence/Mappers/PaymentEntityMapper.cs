using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Mappers;

public static class PaymentEntityMapper
{
    public static Payment ToDomain(PaymentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!Enum.TryParse<PaymentStatus>(
                entity.Status,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted Payment status '{entity.Status}' is invalid.");
        }

        return Payment.Rehydrate(
            entity.PaymentId,
            entity.OrderId,
            entity.Amount,
            entity.Currency,
            status,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.CapturedAtUtc,
            entity.FailureReasonCode,
            entity.Version);
    }

    public static PaymentEntity ToEntity(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PaymentEntity
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString(),
            CreatedAtUtc = payment.CreatedAtUtc,
            UpdatedAtUtc = payment.UpdatedAtUtc,
            CapturedAtUtc = payment.CapturedAtUtc,
            FailureReasonCode = payment.FailureReasonCode,
            Version = payment.Version
        };
    }
}
