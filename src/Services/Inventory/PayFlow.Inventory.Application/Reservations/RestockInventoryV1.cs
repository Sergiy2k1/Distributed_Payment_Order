namespace PayFlow.Inventory.Application.Reservations;

public sealed record RestockInventoryV1(
    Guid OrderId,
    Guid ReservationId,
    Guid RestockOperationId,
    string ReasonCode);
