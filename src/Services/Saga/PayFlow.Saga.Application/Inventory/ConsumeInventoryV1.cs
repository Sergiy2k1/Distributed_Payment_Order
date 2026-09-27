namespace PayFlow.Saga.Application.Inventory;

public sealed record ConsumeInventoryV1(
    Guid OrderId,
    Guid ReservationId);
