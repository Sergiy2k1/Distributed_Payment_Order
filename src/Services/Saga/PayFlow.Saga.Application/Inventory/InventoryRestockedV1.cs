namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryRestockedV1(
    Guid OrderId,
    Guid ReservationId,
    Guid RestockOperationId,
    DateTimeOffset RestockedAtUtc);
