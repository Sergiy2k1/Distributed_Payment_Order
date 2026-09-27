namespace PayFlow.Order.Application.Orders.CancelOrder;

public sealed record CancelOrderV1(
    Guid OrderId,
    string ReasonCode);
