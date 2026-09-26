namespace PayFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class InventoryReservationEntity
{
    public Guid ReservationId { get; set; }

    public Guid OrderId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string? RejectionReasonCode { get; set; }

    public long Version { get; set; }

    public List<InventoryReservationItemEntity> Items { get; set; } = [];
}
