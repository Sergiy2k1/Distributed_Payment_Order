using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryReservationEntityConfiguration
    : IEntityTypeConfiguration<InventoryReservationEntity>
{
    public void Configure(
        EntityTypeBuilder<InventoryReservationEntity> builder)
    {
        builder.ToTable(
            "inventory_reservations",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_inventory_reservations_expiry_after_creation",
                    "expires_at_utc > created_at_utc");

                table.HasCheckConstraint(
                    "ck_inventory_reservations_rejection_reason",
                    "(status = 'Rejected' AND rejection_reason_code IS NOT NULL) OR "
                    + "(status <> 'Rejected' AND rejection_reason_code IS NULL)");
            });

        builder.HasKey(
            reservation => reservation.ReservationId);

        builder.Property(
                reservation => reservation.ReservationId)
            .HasColumnName("reservation_id")
            .ValueGeneratedNever();

        builder.Property(
                reservation => reservation.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(
                reservation => reservation.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(
                reservation => reservation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(
                reservation => reservation.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(
                reservation => reservation.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(
                reservation => reservation.RejectionReasonCode)
            .HasColumnName("rejection_reason_code")
            .HasMaxLength(128);

        builder.Property(
                reservation => reservation.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(
            reservation => reservation.OrderId)
            .HasDatabaseName(
                "IX_inventory_reservations_order_id");

        builder.HasMany(
                reservation => reservation.Items)
            .WithOne(item => item.Reservation)
            .HasForeignKey(
                item => item.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
