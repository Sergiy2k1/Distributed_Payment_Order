using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PayFlow.Order.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Order.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("20260923184500_AddOrderIdempotency")]
partial class AddOrderIdempotency
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
