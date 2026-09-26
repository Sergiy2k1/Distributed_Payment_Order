namespace PayFlow.Saga.Application.Orders;

public sealed record OrderCreatedV1(
    Guid OrderId,
    Guid CustomerId,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<OrderCreatedItemV1> Items);

public sealed record OrderCreatedItemV1(
    string SkuId,
    int Quantity,
    decimal UnitPrice);
