using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Persistence.Configurations;

public sealed class OrderItemEntityConfiguration
    : IEntityTypeConfiguration<OrderItemEntity>
{
    public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
    {
        builder.ToTable(
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

        builder.HasKey(item => new
        {
            item.OrderId,
            item.Position
        });

        builder.Property(item => item.OrderId)
            .HasColumnName("order_id");

        builder.Property(item => item.Position)
            .HasColumnName("position")
            .ValueGeneratedNever();

        builder.Property(item => item.Sku)
            .HasColumnName("sku")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(item => item.UnitPriceAmount)
            .HasColumnName("unit_price_amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.HasOne(item => item.Order)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
