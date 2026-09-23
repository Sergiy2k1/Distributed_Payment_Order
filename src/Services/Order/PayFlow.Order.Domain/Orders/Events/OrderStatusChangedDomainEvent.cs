namespace PayFlow.Order.Domain.Orders.Events;

public sealed record OrderStatusChangedDomainEvent(
    OrderId OrderId,
    OrderStatus PreviousStatus,
    OrderStatus CurrentStatus,
    long Version,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
