using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Abstractions;

public interface ICancelOrderOutboxWriter
{
    Task AddAsync(
        OrderStatusChangedDomainEvent domainEvent,
        string reasonCode,
        Guid correlationId,
        Guid causationId,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
