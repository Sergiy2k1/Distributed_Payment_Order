using PayFlow.Payment.Domain.Payments;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(PaymentAggregate payment, CancellationToken cancellationToken = default);
    Task<PaymentAggregate?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task ApplyAsync(PaymentAggregate payment, CancellationToken cancellationToken = default);
}
