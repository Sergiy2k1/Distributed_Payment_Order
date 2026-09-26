using PayFlow.Saga.Application.Abstractions;

namespace PayFlow.Saga.Infrastructure.Persistence;

public sealed class SagaEfUnitOfWork
    : ISagaUnitOfWork
{
    private readonly SagaDbContext _dbContext;

    public SagaEfUnitOfWork(
        SagaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
