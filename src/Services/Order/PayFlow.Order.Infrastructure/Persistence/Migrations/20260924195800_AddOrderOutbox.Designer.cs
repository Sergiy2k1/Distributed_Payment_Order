using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PayFlow.Order.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Order.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("20260924195800_AddOrderOutbox")]
partial class AddOrderOutbox
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.CreateOrderIdempotencyEntity",
            entity =>
            {
                entity.Property<string>("IdempotencyKey")
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("idempotency_key");

                entity.Property<DateTimeOffset>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at_utc");

                entity.Property<Guid>("OrderId")
                    .HasColumnType("uuid")
                    .HasColumnName("order_id");

                entity.Property<string>("RequestHash")
                    .IsRequired()
                    .IsFixedLength()
                    .HasMaxLength(64)
                    .HasColumnType("character(64)")
                    .HasColumnName("request_hash");

                entity.Property<string>("ResponseCurrency")
                    .IsRequired()
                    .IsFixedLength()
                    .HasMaxLength(3)
                    .HasColumnType("character(3)")
                    .HasColumnName("response_currency");

                entity.Property<string>("ResponseStatus")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)")
                    .HasColumnName("response_status");

                entity.Property<decimal>("ResponseTotalAmount")
                    .HasPrecision(19, 4)
                    .HasColumnType("numeric(19,4)")
                    .HasColumnName("response_total_amount");

                entity.HasKey("IdempotencyKey");

                entity.HasIndex("OrderId")
                    .IsUnique();

                entity.ToTable("order_idempotency");
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.OutboxMessageEntity",
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
                    .HasDatabaseName("IX_outbox_messages_publish_pending")
                    .HasFilter("published_at_utc IS NULL");

                entity.ToTable(
                    "outbox_messages",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_outbox_messages_attempt_count_non_negative",
                            "attempt_count >= 0");

                        table.HasCheckConstraint(
                            "ck_outbox_messages_schema_version_positive",
                            "schema_version > 0");
                    });
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.OrderEntity",
            entity =>
            {
                entity.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                entity.Property<DateTimeOffset>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at_utc");

                entity.Property<string>("Currency")
                    .IsRequired()
                    .IsFixedLength()
                    .HasMaxLength(3)
                    .HasColumnType("character(3)")
                    .HasColumnName("currency");

                entity.Property<Guid>("CustomerId")
                    .HasColumnType("uuid")
                    .HasColumnName("customer_id");

                entity.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)")
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

                entity.HasKey("Id");

                entity.ToTable("orders");
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.OrderItemEntity",
            entity =>
            {
                entity.Property<Guid>("OrderId")
                    .HasColumnType("uuid")
                    .HasColumnName("order_id");

                entity.Property<int>("Position")
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

                entity.Property<string>("Sku")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)")
                    .HasColumnName("sku");

                entity.Property<decimal>("UnitPriceAmount")
                    .HasPrecision(19, 4)
                    .HasColumnType("numeric(19,4)")
                    .HasColumnName("unit_price_amount");

                entity.HasKey("OrderId", "Position");

                entity.ToTable(
                    "order_items",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "ck_order_items_quantity_positive",
                            "quantity > 0");

                        table.HasCheckConstraint(
                            "ck_order_items_unit_price_positive",
                            "unit_price_amount > 0");
                    });
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.CreateOrderIdempotencyEntity",
            entity =>
            {
                entity.HasOne(
                        "PayFlow.Order.Infrastructure.Persistence.Entities.OrderEntity",
                        null)
                    .WithOne()
                    .HasForeignKey(
                        "PayFlow.Order.Infrastructure.Persistence.Entities.CreateOrderIdempotencyEntity",
                        "OrderId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.OrderItemEntity",
            entity =>
            {
                entity.HasOne(
                        "PayFlow.Order.Infrastructure.Persistence.Entities.OrderEntity",
                        "Order")
                    .WithMany("Items")
                    .HasForeignKey("OrderId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Order");
            });

        modelBuilder.Entity(
            "PayFlow.Order.Infrastructure.Persistence.Entities.OrderEntity",
            entity =>
            {
                entity.Navigation("Items");
            });
#pragma warning restore 612, 618
    }
}
