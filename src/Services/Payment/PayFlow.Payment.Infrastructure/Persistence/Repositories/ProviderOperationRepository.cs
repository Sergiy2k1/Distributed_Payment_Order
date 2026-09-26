using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Persistence.Mappers;

namespace PayFlow.Payment.Infrastructure.Persistence.Repositories;

public sealed class ProviderOperationRepository : IProviderOperationRepository
{
    private readonly PaymentDbContext _dbContext;

    public ProviderOperationRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        ProviderOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await _dbContext.ProviderOperations.AddAsync(
            ProviderOperationEntityMapper.ToEntity(operation),
            cancellationToken);
    }

    public async Task<ProviderOperation?> GetByBusinessOperationAsync(
        string operationType,
        Guid businessOperationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        if (businessOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Business operation ID cannot be empty.",
                nameof(businessOperationId));
        }

        var entity = await _dbContext.ProviderOperations
            .SingleOrDefaultAsync(
                operation =>
                    operation.OperationType == operationType
                    && operation.BusinessOperationId == businessOperationId,
                cancellationToken);

        return entity is null
            ? null
            : ProviderOperationEntityMapper.ToDomain(entity);
    }

    public Task ApplyAsync(
        ProviderOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = _dbContext.ProviderOperations.Local
            .SingleOrDefault(
                tracked =>
                    tracked.ProviderOperationId
                    == operation.ProviderOperationId)
            ?? throw new InvalidOperationException(
                "Provider operation must be loaded before applying a transition.");

        entity.Status = operation.Status.ToString();
        entity.AttemptCount = operation.AttemptCount;
        entity.UpdatedAtUtc = operation.UpdatedAtUtc;
        entity.LastAttemptAtUtc = operation.LastAttemptAtUtc;
        entity.NextAttemptAtUtc = operation.NextAttemptAtUtc;
        entity.LastErrorCode = operation.LastErrorCode;
        entity.ProviderReference = operation.ProviderReference;
        entity.Version = operation.Version;

        return Task.CompletedTask;
    }
}
