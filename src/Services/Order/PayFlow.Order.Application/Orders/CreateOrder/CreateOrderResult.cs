using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed record CreateOrderResult(
    Guid OrderId,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency);
