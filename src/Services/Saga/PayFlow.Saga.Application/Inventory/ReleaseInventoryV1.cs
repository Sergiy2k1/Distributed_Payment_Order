namespace PayFlow.Saga.Application.Inventory;

public sealed record ReleaseInventoryV1(
    Guid OrderId,
    Guid ReservationId,
    string ReasonCode);
