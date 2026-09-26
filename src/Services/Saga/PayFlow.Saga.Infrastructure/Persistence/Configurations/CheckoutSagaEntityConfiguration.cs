using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Configurations;

public sealed class CheckoutSagaEntityConfiguration
    : IEntityTypeConfiguration<CheckoutSagaEntity>
{
    public void Configure(
        EntityTypeBuilder<CheckoutSagaEntity> builder)
    {
        builder.ToTable(
            "checkout_sagas",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_checkout_sagas_total_amount_positive",
                    "total_amount > 0");

                table.HasCheckConstraint(
                    "ck_checkout_sagas_retry_count_non_negative",
                    "retry_count >= 0");

                table.HasCheckConstraint(
                    "ck_checkout_sagas_deadline_after_start",
                    "deadline_at_utc > started_at_utc");
            });

        builder.HasKey(saga => saga.OrderId);

        builder.Property(saga => saga.OrderId)
            .HasColumnName("order_id")
            .ValueGeneratedNever();

        builder.Property(saga => saga.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(saga => saga.Status)
            .HasColumnName("status")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(saga => saga.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(saga => saga.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(saga => saga.StartedAtUtc)
            .HasColumnName("started_at_utc")
            .IsRequired();

        builder.Property(saga => saga.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(saga => saga.DeadlineAtUtc)
            .HasColumnName("deadline_at_utc")
            .IsRequired();

        builder.Property(saga => saga.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(saga => saga.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc");

        builder.Property(saga => saga.LastTechnicalErrorCode)
            .HasColumnName("last_technical_error_code")
            .HasMaxLength(128);

        builder.Property(saga => saga.LastTechnicalErrorMessage)
            .HasColumnName("last_technical_error_message")
            .HasMaxLength(2048);

        builder.Property(saga => saga.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(
                saga => new
                {
                    saga.Status,
                    saga.NextAttemptAtUtc
                })
            .HasDatabaseName(
                "IX_checkout_sagas_status_next_attempt_at_utc");
    }
}
