namespace PayFlow.Saga.Application.Inventory;

public sealed record ReserveInventoryV1(
    Guid OrderId,
    Guid ReservationId,
    IReadOnlyList<ReserveInventoryItemV1> Items,
    DateTimeOffset ExpiresAtUtc);

public sealed record ReserveInventoryItemV1(
    string SkuId,
    int Quantity);
