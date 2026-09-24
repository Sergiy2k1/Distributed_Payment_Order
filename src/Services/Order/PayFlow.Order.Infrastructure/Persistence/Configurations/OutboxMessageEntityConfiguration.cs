using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageEntityConfiguration
    : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(
        EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable(
            "outbox_messages",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_outbox_messages_schema_version_positive",
                    "schema_version > 0");

                table.HasCheckConstraint(
                    "ck_outbox_messages_attempt_count_non_negative",
                    "attempt_count >= 0");
            });

        builder.HasKey(message => message.OutboxMessageId);

        builder.Property(message => message.OutboxMessageId)
            .HasColumnName("outbox_message_id")
            .ValueGeneratedNever();

        builder.Property(message => message.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(message => message.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(message => message.SchemaVersion)
            .HasColumnName("schema_version")
            .IsRequired();

        builder.Property(message => message.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();

        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(message => message.CausationId)
            .HasColumnName("causation_id");

        builder.Property(message => message.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.Property(message => message.Destination)
            .HasColumnName("destination")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(message => message.Producer)
            .HasColumnName("producer")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(message => message.TraceParent)
            .HasColumnName("trace_parent")
            .HasMaxLength(256);

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(message => message.PublishedAtUtc)
            .HasColumnName("published_at_utc");

        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc");

        builder.Property(message => message.LastErrorCode)
            .HasColumnName("last_error_code")
            .HasMaxLength(128);

        builder.HasIndex(message => message.MessageId)
            .IsUnique();

        builder.HasIndex(
                message => new
                {
                    message.PublishedAtUtc,
                    message.NextAttemptAtUtc,
                    message.CreatedAtUtc
                })
            .HasDatabaseName("IX_outbox_messages_publish_pending")
            .HasFilter("published_at_utc IS NULL");
    }
}
