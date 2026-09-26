using PayFlow.Payment.Domain.ProviderOperations;

namespace PayFlow.Payment.Application.Abstractions;

public interface IProviderOperationRepository
{
    Task AddAsync(ProviderOperation operation, CancellationToken cancellationToken = default);
    Task<ProviderOperation?> GetByBusinessOperationAsync(
        string operationType,
        Guid businessOperationId,
        CancellationToken cancellationToken = default);
    Task ApplyAsync(ProviderOperation operation, CancellationToken cancellationToken = default);
}
