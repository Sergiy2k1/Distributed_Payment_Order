using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Abstractions;

public interface IOrderCommandOutboxWriter
{
    Task AddAsync(
        OrderStatusChangedDomainEvent domainEvent,
        Guid correlationId,
        Guid causationId,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
