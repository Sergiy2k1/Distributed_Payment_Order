namespace PayFlow.Inventory.Application.Reservations;

public sealed record InventoryRestockedV1(
    Guid OrderId,
    Guid ReservationId,
    Guid RestockOperationId,
    DateTimeOffset RestockedAtUtc);
