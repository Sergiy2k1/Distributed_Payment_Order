using PayFlow.Payment.Application.Abstractions;

namespace PayFlow.Payment.Infrastructure.Persistence;

public sealed class PaymentEfUnitOfWork
    : IPaymentUnitOfWork
{
    private readonly PaymentDbContext _dbContext;

    public PaymentEfUnitOfWork(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
