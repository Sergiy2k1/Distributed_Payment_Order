using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
partial class SagaDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(
        ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation(
                "ProductVersion",
                "10.0.12")
            .HasAnnotation(
                "Relational:MaxIdentifierLength",
                63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(
            modelBuilder);

        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.InboxMessageEntity",
            entity =>
            {
                entity.Property<string>("ConsumerName")
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("consumer_name");

                entity.Property<Guid>("MessageId")
                    .HasColumnType("uuid")
                    .HasColumnName("message_id");

                entity.Property<Guid>("AggregateId")
                    .HasColumnType("uuid")
                    .HasColumnName("aggregate_id");

                entity.Property<Guid?>("CausationId")
                    .HasColumnType("uuid")
                    .HasColumnName("causation_id");

                entity.Property<Guid>("CorrelationId")
                    .HasColumnType("uuid")
                    .HasColumnName("correlation_id");

                entity.Property<string>("MessageType")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("message_type");

                entity.Property<DateTimeOffset>("OccurredAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("occurred_at_utc");

                entity.Property<DateTimeOffset?>("ProcessedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("processed_at_utc");

                entity.Property<DateTimeOffset>("ReceivedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("received_at_utc");

                entity.Property<int>("SchemaVersion")
                    .HasColumnType("integer")
                    .HasColumnName("schema_version");

                entity.Property<long>("SourceOffset")
                    .HasColumnType("bigint")
                    .HasColumnName("source_offset");

                entity.Property<int>("SourcePartition")
                    .HasColumnType("integer")
                    .HasColumnName("source_partition");

                entity.Property<string>("SourceTopic")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("source_topic");

                entity.HasKey(
                    "ConsumerName",
                    "MessageId");

                entity.HasIndex("ProcessedAtUtc");

                entity.HasIndex(
                        "SourceTopic",
                        "SourcePartition",
                        "SourceOffset")
                    .HasDatabaseName(
                        "IX_inbox_messages_source_position");

                entity.ToTable(
                    "inbox_messages",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_inbox_messages_schema_version_positive",
                            "schema_version > 0");

                        table.HasCheckConstraint(
                            "ck_inbox_messages_source_offset_non_negative",
                            "source_offset >= 0");

                        table.HasCheckConstraint(
                            "ck_inbox_messages_source_partition_non_negative",
                            "source_partition >= 0");
                    });
            });
#pragma warning restore 612, 618
    }
}
