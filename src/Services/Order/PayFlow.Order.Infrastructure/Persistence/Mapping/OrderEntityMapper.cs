using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Persistence.Entities;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Infrastructure.Persistence.Mapping;

internal static class OrderEntityMapper
{
    public static OrderAggregate ToDomain(
        OrderEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!Enum.TryParse<OrderStatus>(
                entity.Status,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted Order status '{entity.Status}' is invalid.");
        }

        var items = entity.Items
            .OrderBy(item => item.Position)
            .Select(
                item => OrderItem.Create(
                    Sku.From(item.Sku),
                    item.Quantity,
                    Money.From(
                        item.UnitPriceAmount,
                        item.Currency)))
            .ToArray();

        return OrderAggregate.Rehydrate(
            OrderId.From(entity.Id),
            CustomerId.From(entity.CustomerId),
            items,
            Money.From(
                entity.TotalAmount,
                entity.Currency),
            status,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.Version);
    }

    public static OrderEntity ToEntity(OrderAggregate order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = new OrderEntity
        {
            Id = order.Id.Value,
            CustomerId = order.CustomerId.Value,
            Status = order.Status.ToString(),
            TotalAmount = order.Total.Amount,
            Currency = order.Total.Currency,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Version = order.Version
        };

        for (var position = 0; position < order.Items.Count; position++)
        {
            var item = order.Items[position];

            entity.Items.Add(
                new OrderItemEntity
                {
                    OrderId = order.Id.Value,
                    Position = position,
                    Sku = item.Sku.Value,
                    Quantity = item.Quantity,
                    UnitPriceAmount = item.UnitPrice.Amount,
                    Currency = item.UnitPrice.Currency,
                    Order = entity
                });
        }

        return entity;
    }
}
