using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Persistence.Mappers;

namespace PayFlow.Saga.Infrastructure.Persistence.Repositories;

public sealed class CheckoutSagaRepository
    : ICheckoutSagaRepository
{
    private static readonly string[] TerminalStatuses =
    [
        CheckoutSagaStatus.Completed.ToString(),
        CheckoutSagaStatus.CompletedWithBusinessFailure.ToString(),
        CheckoutSagaStatus.ManualInterventionRequired.ToString()
    ];

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

    public async Task<IReadOnlyList<CheckoutSaga>> GetOverdueActiveAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                nameof(nowUtc));
        }

        if (batchSize <= 0 || batchSize > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be between 1 and 1000.");
        }

        var entities =
            await _dbContext.CheckoutSagas
                .AsNoTracking()
                .Include(saga => saga.Items)
                .Where(
                    saga =>
                        saga.DeadlineAtUtc <= nowUtc
                        && (saga.NextAttemptAtUtc == null
                            || saga.NextAttemptAtUtc <= nowUtc)
                        && !TerminalStatuses.Contains(
                            saga.Status))
                .OrderBy(
                    saga => saga.DeadlineAtUtc)
                .ThenBy(
                    saga => saga.OrderId)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        return entities
            .Select(
                CheckoutSagaEntityMapper.ToDomain)
            .ToArray();
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
        entity.RefundId = saga.RefundId;
        entity.PostCaptureCompensationMode =
            saga.PostCaptureCompensationMode?.ToString();
        entity.RestockOperationId = saga.RestockOperationId;
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
