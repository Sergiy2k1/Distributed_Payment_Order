namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReservationRejectedV1(
    Guid OrderId,
    Guid ReservationId,
    string ReasonCode);
