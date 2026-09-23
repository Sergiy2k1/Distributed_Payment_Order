namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed record CreateOrderItem(
    string Sku,
    int Quantity,
    decimal UnitPrice,
    string Currency);
