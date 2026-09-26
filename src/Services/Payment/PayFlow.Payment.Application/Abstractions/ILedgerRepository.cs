using PayFlow.Payment.Domain.Ledger;

namespace PayFlow.Payment.Application.Abstractions;

public interface ILedgerRepository
{
    Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        string operationType,
        Guid businessOperationId,
        CancellationToken cancellationToken = default);
}
