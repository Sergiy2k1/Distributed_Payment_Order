using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class ProviderOperationEntityConfiguration
    : IEntityTypeConfiguration<ProviderOperationEntity>
{
    public void Configure(
        EntityTypeBuilder<ProviderOperationEntity> builder)
    {
        builder.ToTable(
            "provider_operations",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_provider_operations_attempt_count_non_negative",
                    "attempt_count >= 0");
            });

        builder.HasKey(operation => operation.ProviderOperationId);

        builder.Property(operation => operation.ProviderOperationId)
            .HasColumnName("provider_operation_id")
            .ValueGeneratedNever();

        builder.Property(operation => operation.BusinessOperationId)
            .HasColumnName("business_operation_id")
            .IsRequired();

        builder.Property(operation => operation.OperationType)
            .HasColumnName("operation_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(operation => operation.ProviderIdempotencyKey)
            .HasColumnName("provider_idempotency_key")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(operation => operation.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(operation => operation.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(operation => operation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(operation => operation.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(operation => operation.LastAttemptAtUtc)
            .HasColumnName("last_attempt_at_utc");

        builder.Property(operation => operation.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc");

        builder.Property(operation => operation.LastErrorCode)
            .HasColumnName("last_error_code")
            .HasMaxLength(128);

        builder.Property(operation => operation.ProviderReference)
            .HasColumnName("provider_reference")
            .HasMaxLength(256);

        builder.Property(operation => operation.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(operation => operation.ProviderIdempotencyKey)
            .IsUnique();

        builder.HasIndex(operation => new
            {
                operation.OperationType,
                operation.BusinessOperationId
            })
            .IsUnique()
            .HasDatabaseName(
                "IX_provider_operations_business_identity");

        builder.HasIndex(operation => new
            {
                operation.Status,
                operation.NextAttemptAtUtc
            })
            .HasDatabaseName(
                "IX_provider_operations_reconciliation");
    }
}
