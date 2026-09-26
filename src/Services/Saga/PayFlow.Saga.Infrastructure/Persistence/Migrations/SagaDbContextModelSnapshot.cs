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
        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.CheckoutSagaEntity",
            entity =>
            {
                entity.Property<Guid>("OrderId")
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("order_id");

                entity.Property<string>("Currency")
                    .IsRequired()
                    .IsFixedLength()
                    .HasMaxLength(3)
                    .HasColumnType("character(3)")
                    .HasColumnName("currency");

                entity.Property<Guid>("CustomerId")
                    .HasColumnType("uuid")
                    .HasColumnName("customer_id");

                entity.Property<DateTimeOffset>("DeadlineAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("deadline_at_utc");

                entity.Property<string>("LastTechnicalErrorCode")
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("last_technical_error_code");

                entity.Property<string>("LastTechnicalErrorMessage")
                    .HasMaxLength(2048)
                    .HasColumnType("character varying(2048)")
                    .HasColumnName("last_technical_error_message");

                entity.Property<DateTimeOffset?>("NextAttemptAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("next_attempt_at_utc");

                entity.Property<Guid?>("PaymentId")
                    .HasColumnType("uuid")
                    .HasColumnName("payment_id");

                entity.Property<Guid?>("ReservationId")
                    .HasColumnType("uuid")
                    .HasColumnName("reservation_id");

                entity.Property<DateTimeOffset?>("ReservationExpiresAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("reservation_expires_at_utc");

                entity.Property<int>("RetryCount")
                    .HasColumnType("integer")
                    .HasColumnName("retry_count");

                entity.Property<DateTimeOffset>("StartedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("started_at_utc");

                entity.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)")
                    .HasColumnName("status");

                entity.Property<decimal>("TotalAmount")
                    .HasPrecision(19, 4)
                    .HasColumnType("numeric(19,4)")
                    .HasColumnName("total_amount");

                entity.Property<DateTimeOffset>("UpdatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at_utc");

                entity.Property<long>("Version")
                    .IsConcurrencyToken()
                    .HasColumnType("bigint")
                    .HasColumnName("version");

                entity.HasKey("OrderId");

                entity.HasIndex(
                        "Status",
                        "NextAttemptAtUtc")
                    .HasDatabaseName(
                        "IX_checkout_sagas_status_next_attempt_at_utc");

                entity.ToTable(
                    "checkout_sagas",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_checkout_sagas_deadline_after_start",
                            "deadline_at_utc > started_at_utc");

                        table.HasCheckConstraint(
                            "ck_checkout_sagas_reservation_deadline",
                            "reservation_expires_at_utc IS NULL OR (reservation_expires_at_utc > started_at_utc AND reservation_expires_at_utc <= deadline_at_utc)");

                        table.HasCheckConstraint(
                            "ck_checkout_sagas_reservation_pair",
                            "(reservation_id IS NULL AND reservation_expires_at_utc IS NULL) OR (reservation_id IS NOT NULL AND reservation_expires_at_utc IS NOT NULL)");

                        table.HasCheckConstraint(
                            "ck_checkout_sagas_retry_count_non_negative",
                            "retry_count >= 0");

                        table.HasCheckConstraint(
                            "ck_checkout_sagas_total_amount_positive",
                            "total_amount > 0");
                    });
            });

        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.CheckoutSagaItemEntity",
            entity =>
            {
                entity.Property<Guid>("OrderId")
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("order_id");

                entity.Property<int>("Position")
                    .ValueGeneratedNever()
                    .HasColumnType("integer")
                    .HasColumnName("position");

                entity.Property<string>("Currency")
                    .IsRequired()
                    .IsFixedLength()
                    .HasMaxLength(3)
                    .HasColumnType("character(3)")
                    .HasColumnName("currency");

                entity.Property<int>("Quantity")
                    .HasColumnType("integer")
                    .HasColumnName("quantity");

                entity.Property<string>("SkuId")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("sku_id");

                entity.Property<decimal>("UnitPrice")
                    .HasPrecision(19, 4)
                    .HasColumnType("numeric(19,4)")
                    .HasColumnName("unit_price");

                entity.HasKey(
                    "OrderId",
                    "Position");

                entity.ToTable(
                    "checkout_saga_items",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_checkout_saga_items_quantity_positive",
                            "quantity > 0");

                        table.HasCheckConstraint(
                            "ck_checkout_saga_items_unit_price_positive",
                            "unit_price > 0");
                    });
            });

        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.OutboxMessageEntity",
            entity =>
            {
                entity.Property<Guid>("OutboxMessageId")
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("outbox_message_id");

                entity.Property<Guid>("AggregateId")
                    .HasColumnType("uuid")
                    .HasColumnName("aggregate_id");

                entity.Property<int>("AttemptCount")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("integer")
                    .HasDefaultValue(0)
                    .HasColumnName("attempt_count");

                entity.Property<Guid?>("CausationId")
                    .HasColumnType("uuid")
                    .HasColumnName("causation_id");

                entity.Property<DateTimeOffset?>("ClaimedUntilUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("claimed_until_utc");

                entity.Property<Guid?>("ClaimToken")
                    .HasColumnType("uuid")
                    .HasColumnName("claim_token");

                entity.Property<Guid>("CorrelationId")
                    .HasColumnType("uuid")
                    .HasColumnName("correlation_id");

                entity.Property<DateTimeOffset>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at_utc");

                entity.Property<string>("Destination")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("destination");

                entity.Property<string>("LastErrorCode")
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("last_error_code");

                entity.Property<Guid>("MessageId")
                    .HasColumnType("uuid")
                    .HasColumnName("message_id");

                entity.Property<string>("MessageType")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("message_type");

                entity.Property<DateTimeOffset?>("NextAttemptAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("next_attempt_at_utc");

                entity.Property<DateTimeOffset>("OccurredAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("occurred_at_utc");

                entity.Property<string>("Payload")
                    .IsRequired()
                    .HasColumnType("jsonb")
                    .HasColumnName("payload");

                entity.Property<string>("Producer")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("producer");

                entity.Property<DateTimeOffset?>("PublishedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("published_at_utc");

                entity.Property<int>("SchemaVersion")
                    .HasColumnType("integer")
                    .HasColumnName("schema_version");

                entity.Property<string>("TraceParent")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("trace_parent");

                entity.HasKey("OutboxMessageId");

                entity.HasIndex("MessageId")
                    .IsUnique();

                entity.HasIndex(
                        "PublishedAtUtc",
                        "NextAttemptAtUtc",
                        "CreatedAtUtc")
                    .HasDatabaseName(
                        "IX_outbox_messages_publish_pending")
                    .HasFilter("published_at_utc IS NULL");

                entity.ToTable(
                    "outbox_messages",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_outbox_messages_attempt_count_non_negative",
                            "attempt_count >= 0");

                        table.HasCheckConstraint(
                            "ck_outbox_messages_claim_pair",
                            "(claim_token IS NULL AND claimed_until_utc IS NULL) OR (claim_token IS NOT NULL AND claimed_until_utc IS NOT NULL)");

                        table.HasCheckConstraint(
                            "ck_outbox_messages_schema_version_positive",
                            "schema_version > 0");
                    });
            });

        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.CheckoutSagaItemEntity",
            entity =>
            {
                entity.HasOne(
                        "PayFlow.Saga.Infrastructure.Persistence.Entities.CheckoutSagaEntity",
                        "Saga")
                    .WithMany("Items")
                    .HasForeignKey("OrderId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Saga");
            });

        modelBuilder.Entity(
            "PayFlow.Saga.Infrastructure.Persistence.Entities.CheckoutSagaEntity",
            entity =>
            {
                entity.Navigation("Items");
            });
#pragma warning restore 612, 618
    }
}
