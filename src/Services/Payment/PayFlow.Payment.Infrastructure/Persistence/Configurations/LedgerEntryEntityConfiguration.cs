using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryEntityConfiguration
    : IEntityTypeConfiguration<LedgerEntryEntity>
{
    public void Configure(
        EntityTypeBuilder<LedgerEntryEntity> builder)
    {
        builder.ToTable(
            "ledger_entries",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_ledger_entries_amount_positive",
                    "amount > 0");
            });

        builder.HasKey(entry => entry.LedgerEntryId);

        builder.Property(entry => entry.LedgerEntryId)
            .HasColumnName("ledger_entry_id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.LedgerTransactionId)
            .HasColumnName("ledger_transaction_id")
            .IsRequired();

        builder.Property(entry => entry.AccountId)
            .HasColumnName("account_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(entry => entry.Side)
            .HasColumnName("side")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(entry => entry.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();
    }
}
