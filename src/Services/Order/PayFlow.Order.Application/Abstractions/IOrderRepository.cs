using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(
        OrderAggregate order,
        CancellationToken cancellationToken = default);
}
