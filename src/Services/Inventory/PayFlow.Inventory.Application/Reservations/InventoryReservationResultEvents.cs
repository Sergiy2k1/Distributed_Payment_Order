namespace PayFlow.Inventory.Application.Reservations;

public sealed record InventoryReservedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ReservedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record InventoryReservationRejectedV1(
    Guid OrderId,
    Guid ReservationId,
    string ReasonCode);
