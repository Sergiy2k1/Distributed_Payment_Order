namespace PayFlow.Order.Api.Endpoints.Orders.CreateOrder;

public sealed record CreateOrderResponse(
    Guid OrderId,
    string Status,
    decimal TotalAmount,
    string Currency);
