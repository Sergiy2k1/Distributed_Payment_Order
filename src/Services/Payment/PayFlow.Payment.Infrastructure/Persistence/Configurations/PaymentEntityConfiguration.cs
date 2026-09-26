using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class PaymentEntityConfiguration
    : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(
        EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable(
            "payments",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_payments_amount_positive",
                    "amount > 0");
            });

        builder.HasKey(payment => payment.PaymentId);

        builder.Property(payment => payment.PaymentId)
            .HasColumnName("payment_id")
            .ValueGeneratedNever();

        builder.Property(payment => payment.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(payment => payment.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(payment => payment.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(payment => payment.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(payment => payment.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(payment => payment.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(payment => payment.CapturedAtUtc)
            .HasColumnName("captured_at_utc");

        builder.Property(payment => payment.FailureReasonCode)
            .HasColumnName("failure_reason_code")
            .HasMaxLength(128);

        builder.Property(payment => payment.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(payment => payment.OrderId)
            .HasDatabaseName("IX_payments_order_id");
    }
}
