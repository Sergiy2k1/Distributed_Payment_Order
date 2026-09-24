using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Abstractions;

public interface IOutboxWriter
{
    Task AddAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default);
}
