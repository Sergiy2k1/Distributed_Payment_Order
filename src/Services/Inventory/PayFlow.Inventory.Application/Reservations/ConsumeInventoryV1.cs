namespace PayFlow.Inventory.Application.Reservations;

public sealed record ConsumeInventoryV1(
    Guid OrderId,
    Guid ReservationId);
