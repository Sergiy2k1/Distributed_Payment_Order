namespace PayFlow.Saga.Application.Inventory;

public sealed record RestockInventoryV1(
    Guid OrderId,
    Guid ReservationId,
    Guid RestockOperationId,
    string ReasonCode);
