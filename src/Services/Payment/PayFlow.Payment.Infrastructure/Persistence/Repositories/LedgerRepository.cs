using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain.Ledger;
using PayFlow.Payment.Infrastructure.Persistence.Mappers;

namespace PayFlow.Payment.Infrastructure.Persistence.Repositories;

public sealed class LedgerRepository : ILedgerRepository
{
    private readonly PaymentDbContext _dbContext;

    public LedgerRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _dbContext.LedgerTransactions.AddAsync(
            LedgerEntityMapper.ToEntity(transaction),
            cancellationToken);
    }

    public Task<bool> ExistsAsync(
        string operationType,
        Guid businessOperationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        return _dbContext.LedgerTransactions.AnyAsync(
            transaction =>
                transaction.OperationType == operationType
                && transaction.BusinessOperationId == businessOperationId,
            cancellationToken);
    }
}
