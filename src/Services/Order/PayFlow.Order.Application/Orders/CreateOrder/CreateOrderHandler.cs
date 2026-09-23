using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IClock _clock;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _clock = clock;
    }

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Items);

        var items = command.Items
            .Select(static item =>
            {
                ArgumentNullException.ThrowIfNull(item);

                return OrderItem.Create(
                    Sku.From(item.Sku),
                    item.Quantity,
                    Money.From(item.UnitPrice, item.Currency));
            })
            .ToArray();

        var order = OrderAggregate.Create(
            OrderId.New(),
            CustomerId.From(command.CustomerId),
            items,
            _clock.UtcNow);

        await _orderRepository
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

        return new CreateOrderResult(
            order.Id.Value,
            order.Status,
            order.Total.Amount,
            order.Total.Currency);
    }
}
