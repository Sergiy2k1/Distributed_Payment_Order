using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageEntityConfiguration
    : IEntityTypeConfiguration<InboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<InboxMessageEntity> builder)
    {
        builder.ToTable(
            "inbox_messages",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_inbox_messages_schema_version_positive",
                    "schema_version > 0");
                table.HasCheckConstraint(
                    "ck_inbox_messages_source_partition_non_negative",
                    "source_partition >= 0");
                table.HasCheckConstraint(
                    "ck_inbox_messages_source_offset_non_negative",
                    "source_offset >= 0");
            });

        builder.HasKey(message => new { message.ConsumerName, message.MessageId });

        builder.Property(message => message.ConsumerName).HasColumnName("consumer_name").HasMaxLength(128).IsRequired();
        builder.Property(message => message.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(message => message.MessageType).HasColumnName("message_type").HasMaxLength(256).IsRequired();
        builder.Property(message => message.SchemaVersion).HasColumnName("schema_version").IsRequired();
        builder.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(message => message.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(message => message.CausationId).HasColumnName("causation_id");
        builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(message => message.SourceTopic).HasColumnName("source_topic").HasMaxLength(256).IsRequired();
        builder.Property(message => message.SourcePartition).HasColumnName("source_partition").IsRequired();
        builder.Property(message => message.SourceOffset).HasColumnName("source_offset").IsRequired();
        builder.Property(message => message.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
        builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc");

        builder.HasIndex(message => new { message.SourceTopic, message.SourcePartition, message.SourceOffset })
            .HasDatabaseName("IX_inbox_messages_source_position");

        builder.HasIndex(message => message.ProcessedAtUtc);
    }
}
