namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReservedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ReservedAtUtc,
    DateTimeOffset ExpiresAtUtc);
