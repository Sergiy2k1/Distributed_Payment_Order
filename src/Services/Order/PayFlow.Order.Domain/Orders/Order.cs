using System.Collections.ObjectModel;

namespace PayFlow.Order.Domain.Orders;

public sealed class Order
{
    private Order(
        OrderId id,
        CustomerId customerId,
        ReadOnlyCollection<OrderItem> items,
        Money total)
    {
        Id = id;
        CustomerId = customerId;
        Items = items;
        Total = total;
        Status = OrderStatus.Pending;
    }

    public OrderId Id { get; }

    public CustomerId CustomerId { get; }

    public IReadOnlyList<OrderItem> Items { get; }

    public Money Total { get; }

    public OrderStatus Status { get; private set; }

    public static Order Create(
        OrderId id,
        CustomerId customerId,
        IEnumerable<OrderItem> items)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(id));
        }

        if (customerId.Value == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(items);

        var itemArray = items.ToArray();

        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Order must contain at least one item.",
                nameof(items));
        }

        var currency = itemArray[0].UnitPrice.Currency;

        if (itemArray.Any(item =>
                !string.Equals(
                    item.UnitPrice.Currency,
                    currency,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "All order items must use the same currency.",
                nameof(items));
        }

        var total = Money.Zero(currency);

        foreach (var item in itemArray)
        {
            total = total.Add(item.LineTotal);
        }

        return new Order(
            id,
            customerId,
            Array.AsReadOnly(itemArray),
            total);
    }

    public void StartProcessing()
    {
        TransitionTo(OrderStatus.Processing, OrderStatus.Pending);
    }

    public void BeginCancellation()
    {
        TransitionTo(OrderStatus.Cancelling, OrderStatus.Pending, OrderStatus.Processing);
    }

    public void Confirm()
    {
        TransitionTo(OrderStatus.Confirmed, OrderStatus.Processing);
    }

    public void CompleteCancellation()
    {
        TransitionTo(OrderStatus.Cancelled, OrderStatus.Cancelling);
    }

    public void RequestRefund()
    {
        TransitionTo(OrderStatus.RefundRequested, OrderStatus.Confirmed);
    }

    public void CompleteRefund()
    {
        TransitionTo(OrderStatus.Refunded, OrderStatus.RefundRequested);
    }

    public void Fulfill()
    {
        TransitionTo(OrderStatus.Fulfilled, OrderStatus.Confirmed);
    }

    private void TransitionTo(OrderStatus target, params OrderStatus[] allowedSources)
    {
        if (Status == target)
        {
            return;
        }

        if (!allowedSources.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Order cannot transition from {Status} to {target}.");
        }

        Status = target;
    }
}
