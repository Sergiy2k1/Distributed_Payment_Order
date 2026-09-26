using PayFlow.Order.Domain.Orders;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Application.Abstractions;

public interface IOrderCommandRepository
{
    Task<OrderAggregate?> GetByIdAsync(
        OrderId orderId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        OrderAggregate order,
        CancellationToken cancellationToken = default);
}
