using Microsoft.EntityFrameworkCore;
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

    public async Task<CheckoutSaga?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order ID cannot be empty.",
                nameof(orderId));
        }

        var entity = await _dbContext.CheckoutSagas
            .Include(saga => saga.Items)
            .SingleOrDefaultAsync(
                saga => saga.OrderId == orderId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : CheckoutSagaEntityMapper.ToDomain(entity);
    }

    public Task ApplyAsync(
        CheckoutSaga saga,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saga);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = _dbContext.CheckoutSagas.Local
            .SingleOrDefault(
                tracked => tracked.OrderId == saga.OrderId)
            ?? throw new InvalidOperationException(
                "Checkout Saga must be loaded by this repository before applying a transition.");

        entity.Status = saga.Status.ToString();
        entity.UpdatedAtUtc = saga.UpdatedAtUtc;
        entity.ReservationId = saga.ReservationId;
        entity.ReservationExpiresAtUtc =
            saga.ReservationExpiresAtUtc;
        entity.PaymentId = saga.PaymentId;
        entity.RetryCount = saga.RetryCount;
        entity.NextAttemptAtUtc = saga.NextAttemptAtUtc;
        entity.LastTechnicalErrorCode =
            saga.LastTechnicalErrorCode;
        entity.LastTechnicalErrorMessage =
            saga.LastTechnicalErrorMessage;
        entity.Version = saga.Version;

        return Task.CompletedTask;
    }
}
