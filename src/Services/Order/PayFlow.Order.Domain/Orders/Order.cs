using System.Collections.ObjectModel;
using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Domain.Orders;

public sealed class Order
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Order(
        OrderId id,
        CustomerId customerId,
        ReadOnlyCollection<OrderItem> items,
        Money total,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        Items = items;
        Total = total;
        Status = OrderStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Version = 0;
    }

    public OrderId Id { get; }

    public CustomerId CustomerId { get; }

    public IReadOnlyList<OrderItem> Items { get; }

    public Money Total { get; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    public static Order Create(
        OrderId id,
        CustomerId customerId,
        IEnumerable<OrderItem> items,
        DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(id));
        }

        if (customerId.Value == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
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

        var orderItems = Array.AsReadOnly(itemArray);

        var order = new Order(
            id,
            customerId,
            orderItems,
            total,
            createdAtUtc);

        order.RaiseDomainEvent(
            new OrderCreatedDomainEvent(
                id,
                customerId,
                orderItems,
                total,
                createdAtUtc));

        return order;
    }

    public void StartProcessing(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.Processing, occurredAtUtc, OrderStatus.Pending);
    }

    public void BeginCancellation(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(
            OrderStatus.Cancelling,
            occurredAtUtc,
            OrderStatus.Pending,
            OrderStatus.Processing);
    }

    public void Confirm(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.Confirmed, occurredAtUtc, OrderStatus.Processing);
    }

    public void CompleteCancellation(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.Cancelled, occurredAtUtc, OrderStatus.Cancelling);
    }

    public void RequestRefund(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.RefundRequested, occurredAtUtc, OrderStatus.Confirmed);
    }

    public void CompleteRefund(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.Refunded, occurredAtUtc, OrderStatus.RefundRequested);
    }

    public void Fulfill(DateTimeOffset occurredAtUtc)
    {
        TransitionTo(OrderStatus.Fulfilled, occurredAtUtc, OrderStatus.Confirmed);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void TransitionTo(
        OrderStatus target,
        DateTimeOffset occurredAtUtc,
        params OrderStatus[] allowedSources)
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

        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (occurredAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAtUtc),
                occurredAtUtc,
                "Order transition timestamp cannot be earlier than the current update timestamp.");
        }

        var previousStatus = Status;

        Status = target;
        UpdatedAtUtc = occurredAtUtc;
        Version++;

        RaiseDomainEvent(
            new OrderStatusChangedDomainEvent(
                Id,
                previousStatus,
                target,
                Version,
                occurredAtUtc));
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    private static void EnsureUtc(DateTimeOffset value, string paramName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                paramName);
        }
    }
}
