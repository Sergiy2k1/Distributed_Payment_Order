using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Persistence.Mappers;

namespace PayFlow.Saga.Infrastructure.Persistence.Repositories;

public sealed class CheckoutSagaRepository
    : ICheckoutSagaRepository
{
    private readonly SagaDbContext _dbContext;

    public CheckoutSagaRepository(
        SagaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        CheckoutSaga saga,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saga);

        await _dbContext.CheckoutSagas
            .AddAsync(
                CheckoutSagaEntityMapper.ToEntity(saga),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
