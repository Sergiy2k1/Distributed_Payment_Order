using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryReservationItemEntityConfiguration
    : IEntityTypeConfiguration<InventoryReservationItemEntity>
{
    public void Configure(
        EntityTypeBuilder<InventoryReservationItemEntity> builder)
    {
        builder.ToTable(
            "inventory_reservation_items",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_reservation_items_quantity_positive",
                    "quantity > 0");
            });

        builder.HasKey(item => new
        {
            item.ReservationId,
            item.Position
        });

        builder.Property(item => item.ReservationId)
            .HasColumnName("reservation_id")
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
    }
}
