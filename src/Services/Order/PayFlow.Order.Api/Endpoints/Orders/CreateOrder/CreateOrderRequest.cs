namespace PayFlow.Order.Api.Endpoints.Orders.CreateOrder;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(
    string Sku,
    int Quantity,
    decimal UnitPrice,
    string Currency);
