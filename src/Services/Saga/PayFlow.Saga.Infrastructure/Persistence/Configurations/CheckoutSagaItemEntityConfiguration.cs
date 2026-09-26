using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Configurations;

public sealed class CheckoutSagaItemEntityConfiguration
    : IEntityTypeConfiguration<CheckoutSagaItemEntity>
{
    public void Configure(
        EntityTypeBuilder<CheckoutSagaItemEntity> builder)
    {
        builder.ToTable(
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

        builder.HasKey(
            item => new
            {
                item.OrderId,
                item.Position
            });

        builder.Property(item => item.OrderId)
            .HasColumnName("order_id")
            .ValueGeneratedNever();

        builder.Property(item => item.Position)
            .HasColumnName("position")
            .ValueGeneratedNever();

        builder.Property(item => item.SkuId)
            .HasColumnName("sku_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(item => item.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(item => item.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.HasOne(item => item.Saga)
            .WithMany(saga => saga.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
