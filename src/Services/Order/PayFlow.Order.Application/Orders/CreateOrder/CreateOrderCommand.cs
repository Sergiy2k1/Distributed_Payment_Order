namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItem> Items);
