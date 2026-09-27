namespace PayFlow.Inventory.Application.Reservations;

public sealed record ReleaseInventoryV1(
    Guid OrderId,
    Guid ReservationId,
    string ReasonCode);
