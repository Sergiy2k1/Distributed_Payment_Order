namespace PayFlow.Inventory.Application.Reservations;

public sealed record InventoryConsumedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ConsumedAtUtc);
