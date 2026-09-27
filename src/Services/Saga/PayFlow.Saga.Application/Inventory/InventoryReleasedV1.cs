namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReleasedV1(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ReleasedAtUtc,
    string ReasonCode);
