namespace PayFlow.Payment.Application.Abstractions;

public interface IPaymentUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
