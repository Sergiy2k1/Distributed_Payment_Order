using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class ProviderWebhookInboxEntityConfiguration
    : IEntityTypeConfiguration<ProviderWebhookInboxEntity>
{
    public void Configure(
        EntityTypeBuilder<ProviderWebhookInboxEntity> builder)
    {
        builder.ToTable(
            "provider_webhook_inbox",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_provider_webhook_inbox_payload_hash_length",
                    "char_length(payload_hash) = 64");
            });

        builder.HasKey(
            webhook => webhook.EventId);

        builder.Property(
                webhook => webhook.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();

        builder.Property(
                webhook => webhook.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(
                webhook => webhook.OperationType)
            .HasColumnName("operation_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(
                webhook => webhook.PaymentId)
            .HasColumnName("payment_id")
            .IsRequired();

        builder.Property(
                webhook => webhook.RefundId)
            .HasColumnName("refund_id");

        builder.Property(
                webhook => webhook.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(
                webhook => webhook.ProviderReference)
            .HasColumnName("provider_reference")
            .HasMaxLength(256);

        builder.Property(
                webhook => webhook.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(128);

        builder.Property(
                webhook => webhook.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.Property(
                webhook => webhook.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .IsRequired();

        builder.Property(
                webhook => webhook.ProcessedAtUtc)
            .HasColumnName("processed_at_utc");

        builder.Property(
                webhook => webhook.PayloadHash)
            .HasColumnName("payload_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.HasIndex(
                webhook => webhook.ProcessedAtUtc)
            .HasDatabaseName(
                "IX_provider_webhook_inbox_unprocessed");

        builder.HasIndex(
                webhook => new
                {
                    webhook.OperationType,
                    webhook.PaymentId
                })
            .HasDatabaseName(
                "IX_provider_webhook_inbox_operation_payment");
    }
}
