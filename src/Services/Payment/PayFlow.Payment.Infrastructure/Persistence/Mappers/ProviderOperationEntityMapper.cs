using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Mappers;

public static class ProviderOperationEntityMapper
{
    public static ProviderOperation ToDomain(ProviderOperationEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!Enum.TryParse<ProviderOperationStatus>(
                entity.Status,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted provider operation status '{entity.Status}' is invalid.");
        }

        return ProviderOperation.Rehydrate(
            entity.ProviderOperationId,
            entity.BusinessOperationId,
            entity.OperationType,
            entity.ProviderIdempotencyKey,
            status,
            entity.AttemptCount,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LastAttemptAtUtc,
            entity.NextAttemptAtUtc,
            entity.LastErrorCode,
            entity.ProviderReference,
            entity.Version);
    }

    public static ProviderOperationEntity ToEntity(ProviderOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new ProviderOperationEntity
        {
            ProviderOperationId = operation.ProviderOperationId,
            BusinessOperationId = operation.BusinessOperationId,
            OperationType = operation.OperationType,
            ProviderIdempotencyKey = operation.ProviderIdempotencyKey,
            Status = operation.Status.ToString(),
            AttemptCount = operation.AttemptCount,
            CreatedAtUtc = operation.CreatedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc,
            LastAttemptAtUtc = operation.LastAttemptAtUtc,
            NextAttemptAtUtc = operation.NextAttemptAtUtc,
            LastErrorCode = operation.LastErrorCode,
            ProviderReference = operation.ProviderReference,
            Version = operation.Version
        };
    }
}
