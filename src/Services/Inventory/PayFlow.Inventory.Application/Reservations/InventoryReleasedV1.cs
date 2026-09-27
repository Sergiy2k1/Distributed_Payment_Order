namespace PayFlow.Inventory.Application.Reservations;

public sealed record InventoryReleasedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ReleasedAtUtc,
    string ReasonCode);
