using PayFlow.Payment.Domain.Payments;

namespace PayFlow.Payment.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task ApplyAsync(Payment payment, CancellationToken cancellationToken = default);
}
