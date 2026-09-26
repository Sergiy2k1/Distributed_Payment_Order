using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Persistence.Mappers;

namespace PayFlow.Payment.Infrastructure.Persistence.Repositories;

public sealed class ProviderOperationExecutionRepository
    : IProviderOperationExecutionRepository
{
    private readonly PaymentDbContext _dbContext;

    public ProviderOperationExecutionRepository(
        PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProviderCaptureWorkItem?> ClaimNextCaptureAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset staleProcessingBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureUtc(
            staleProcessingBeforeUtc,
            nameof(staleProcessingBeforeUtc));

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            staleProcessingBeforeUtc,
            nowUtc);

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

        var entity = await _dbContext.ProviderOperations
            .FromSqlInterpolated(
                $"""
                 SELECT po.*
                 FROM provider_operations AS po
                 INNER JOIN payments AS p
                     ON p.payment_id = po.business_operation_id
                 WHERE po.operation_type = 'Capture'
                   AND p.status = 'Processing'
                   AND
                   (
                       po.status = 'Pending'
                       OR
                       (
                           po.status = 'Ambiguous'
                           AND
                           (
                               po.next_attempt_at_utc IS NULL
                               OR po.next_attempt_at_utc <= {nowUtc}
                           )
                       )
                       OR
                       (
                           po.status = 'Processing'
                           AND po.last_attempt_at_utc IS NOT NULL
                           AND po.last_attempt_at_utc <= {staleProcessingBeforeUtc}
                       )
                   )
                 ORDER BY po.created_at_utc, po.provider_operation_id
                 FOR UPDATE OF po SKIP LOCKED
                 LIMIT 1
                 """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);

            return null;
        }

        var operation =
            ProviderOperationEntityMapper.ToDomain(entity);

        operation.BeginAttempt(nowUtc);

        entity.Status = operation.Status.ToString();
        entity.AttemptCount = operation.AttemptCount;
        entity.UpdatedAtUtc = operation.UpdatedAtUtc;
        entity.LastAttemptAtUtc = operation.LastAttemptAtUtc;
        entity.NextAttemptAtUtc = operation.NextAttemptAtUtc;
        entity.LastErrorCode = operation.LastErrorCode;
        entity.ProviderReference = operation.ProviderReference;
        entity.Version = operation.Version;

        var payment = await _dbContext.Payments
            .AsNoTracking()
            .SingleAsync(
                candidate =>
                    candidate.PaymentId
                    == operation.BusinessOperationId,
                cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ProviderCaptureWorkItem(
            operation.ProviderOperationId,
            payment.PaymentId,
            payment.OrderId,
            payment.Amount,
            payment.Currency,
            operation.ProviderIdempotencyKey,
            operation.AttemptCount,
            operation.CorrelationId,
            operation.CausationId,
            operation.TraceParent);
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
