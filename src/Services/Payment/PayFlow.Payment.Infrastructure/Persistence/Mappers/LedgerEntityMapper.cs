using PayFlow.Payment.Domain.Ledger;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Mappers;

public static class LedgerEntityMapper
{
    public static LedgerTransactionEntity ToEntity(LedgerTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new LedgerTransactionEntity
        {
            LedgerTransactionId = transaction.LedgerTransactionId,
            OperationType = transaction.OperationType,
            BusinessOperationId = transaction.BusinessOperationId,
            Currency = transaction.Currency,
            OccurredAtUtc = transaction.OccurredAtUtc,
            Entries = transaction.Entries
                .Select(entry => new LedgerEntryEntity
                {
                    LedgerEntryId = entry.LedgerEntryId,
                    LedgerTransactionId = transaction.LedgerTransactionId,
                    AccountId = entry.AccountId,
                    Side = entry.Side.ToString(),
                    Amount = entry.Amount,
                    Currency = entry.Currency
                })
                .ToList()
        };
    }
}
