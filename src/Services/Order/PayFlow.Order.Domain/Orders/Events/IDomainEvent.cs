namespace PayFlow.Order.Domain.Orders.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
