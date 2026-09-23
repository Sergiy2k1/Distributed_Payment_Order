namespace PayFlow.Order.Domain.Orders.Events;

public sealed record OrderCreatedDomainEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    IReadOnlyList<OrderItem> Items,
    Money Total,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
