namespace PayFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class InventoryReservationItemEntity
{
    public Guid ReservationId { get; set; }

    public int Position { get; set; }

    public string SkuId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public InventoryReservationEntity Reservation { get; set; } = null!;
}
