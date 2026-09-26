using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemEntityConfiguration
    : IEntityTypeConfiguration<StockItemEntity>
{
    public void Configure(
        EntityTypeBuilder<StockItemEntity> builder)
    {
        builder.ToTable(
            "inventory_stock",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_stock_on_hand_non_negative",
                    "on_hand >= 0");

                table.HasCheckConstraint(
                    "ck_inventory_stock_reserved_range",
                    "reserved >= 0 AND reserved <= on_hand");
            });

        builder.HasKey(stock => stock.SkuId);

        builder.Property(stock => stock.SkuId)
            .HasColumnName("sku_id")
            .HasMaxLength(128)
            .ValueGeneratedNever();

        builder.Property(stock => stock.OnHand)
            .HasColumnName("on_hand")
            .IsRequired();

        builder.Property(stock => stock.Reserved)
            .HasColumnName("reserved")
            .IsRequired();

        builder.Property(stock => stock.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();
    }
}
