namespace PayFlow.Inventory.Domain.Reservations;

public enum InventoryReservationStatus
{
    Pending = 0,
    Reserved = 1,
    Rejected = 2,
    Consumed = 3,
    Released = 4,
    Expired = 5
}
