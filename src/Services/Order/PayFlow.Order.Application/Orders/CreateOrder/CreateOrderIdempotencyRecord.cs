namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed record CreateOrderIdempotencyRecord(
    string IdempotencyKey,
    string RequestHash,
    CreateOrderResult Result,
    DateTimeOffset CreatedAtUtc);
