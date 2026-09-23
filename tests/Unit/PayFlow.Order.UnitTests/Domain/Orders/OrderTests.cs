using PayFlow.Order.Domain.Orders;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class OrderTests
{
    [Fact]
    public void CreateInitializesPendingOrder()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();

        var order = OrderAggregate.Create(orderId, customerId);

        Assert.Equal(orderId, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void CheckoutHappyPathReachesConfirmed()
    {
        var order = CreateOrder();

        order.StartProcessing();
        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void PendingOrderCanBeCancelled()
    {
        var order = CreateOrder();

        order.BeginCancellation();
        order.CompleteCancellation();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ProcessingOrderCanBeCancelled()
    {
        var order = CreateOrder();
        order.StartProcessing();

        order.BeginCancellation();
        order.CompleteCancellation();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ConfirmedOrderCanBeRefunded()
    {
        var order = CreateConfirmedOrder();

        order.RequestRefund();
        order.CompleteRefund();

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void ConfirmedOrderCanBeFulfilled()
    {
        var order = CreateConfirmedOrder();

        order.Fulfill();

        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void RepeatingSameTransitionIsIdempotent()
    {
        var order = CreateOrder();

        order.StartProcessing();
        order.StartProcessing();

        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public void ConfirmFromPendingIsRejected()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<InvalidOperationException>(order.Confirm);

        Assert.Equal("Order cannot transition from Pending to Confirmed.", exception.Message);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void CancelledOrderRejectsFurtherBusinessTransition()
    {
        var order = CreateOrder();
        order.BeginCancellation();
        order.CompleteCancellation();

        Assert.Throws<InvalidOperationException>(order.StartProcessing);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void RefundedOrderRejectsFulfillment()
    {
        var order = CreateConfirmedOrder();
        order.RequestRefund();
        order.CompleteRefund();

        Assert.Throws<InvalidOperationException>(order.Fulfill);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    private static OrderAggregate CreateOrder()
    {
        return OrderAggregate.Create(OrderId.New(), CustomerId.New());
    }

    private static OrderAggregate CreateConfirmedOrder()
    {
        var order = CreateOrder();
        order.StartProcessing();
        order.Confirm();
        return order;
    }
}
