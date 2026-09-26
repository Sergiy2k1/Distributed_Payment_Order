using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class LedgerTransactionEntityConfiguration
    : IEntityTypeConfiguration<LedgerTransactionEntity>
{
    public void Configure(
        EntityTypeBuilder<LedgerTransactionEntity> builder)
    {
        builder.ToTable("ledger_transactions");

        builder.HasKey(
            transaction => transaction.LedgerTransactionId);

        builder.Property(
                transaction => transaction.LedgerTransactionId)
            .HasColumnName("ledger_transaction_id")
            .ValueGeneratedNever();

        builder.Property(transaction => transaction.OperationType)
            .HasColumnName("operation_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transaction => transaction.BusinessOperationId)
            .HasColumnName("business_operation_id")
            .IsRequired();

        builder.Property(transaction => transaction.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(transaction => transaction.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.HasIndex(transaction => new
            {
                transaction.OperationType,
                transaction.BusinessOperationId
            })
            .IsUnique()
            .HasDatabaseName(
                "IX_ledger_transactions_business_identity");

        builder.HasMany(transaction => transaction.Entries)
            .WithOne(entry => entry.Transaction)
            .HasForeignKey(entry => entry.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
