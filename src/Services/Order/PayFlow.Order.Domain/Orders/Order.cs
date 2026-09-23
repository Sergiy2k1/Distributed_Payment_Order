namespace PayFlow.Order.Domain.Orders;

public sealed class Order
{
    private Order(OrderId id, CustomerId customerId)
    {
        Id = id;
        CustomerId = customerId;
        Status = OrderStatus.Pending;
    }

    public OrderId Id { get; }

    public CustomerId CustomerId { get; }

    public OrderStatus Status { get; private set; }

    public static Order Create(OrderId id, CustomerId customerId)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(id));
        }

        if (customerId.Value == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        return new Order(id, customerId);
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
