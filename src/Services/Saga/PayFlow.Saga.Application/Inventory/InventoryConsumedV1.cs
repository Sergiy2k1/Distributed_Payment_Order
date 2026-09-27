namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryConsumedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ConsumedAtUtc);
