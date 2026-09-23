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
        var item = CreateItem("SKU-001", 1, 10m, "USD");

        var order = OrderAggregate.Create(orderId, customerId, [item]);

        Assert.Equal(orderId, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(order.Items);
        Assert.Same(item, order.Items[0]);
        Assert.Equal(Money.From(10m, "USD"), order.Total);
    }

    [Fact]
    public void CreateRejectsEmptyItems()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OrderAggregate.Create(
                OrderId.New(),
                CustomerId.New(),
                []));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsMixedCurrencies()
    {
        var items = new[]
        {
            CreateItem("SKU-001", 1, 10m, "USD"),
            CreateItem("SKU-002", 1, 20m, "EUR")
        };

        var exception = Assert.Throws<ArgumentException>(
            () => OrderAggregate.Create(
                OrderId.New(),
                CustomerId.New(),
                items));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void CreateCalculatesTotalAcrossItems()
    {
        var items = new[]
        {
            CreateItem("SKU-001", 2, 10m, "USD"),
            CreateItem("SKU-002", 3, 5m, "USD")
        };

        var order = OrderAggregate.Create(
            OrderId.New(),
            CustomerId.New(),
            items);

        Assert.Equal(Money.From(35m, "USD"), order.Total);
    }

    [Fact]
    public void CreateCopiesInputCollection()
    {
        var items = new List<OrderItem>
        {
            CreateItem("SKU-001", 1, 10m, "USD")
        };

        var order = OrderAggregate.Create(
            OrderId.New(),
            CustomerId.New(),
            items);

        items.Add(CreateItem("SKU-002", 1, 20m, "USD"));

        Assert.Single(order.Items);
        Assert.Equal(Money.From(10m, "USD"), order.Total);
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
        return OrderAggregate.Create(
            OrderId.New(),
            CustomerId.New(),
            [CreateItem("SKU-001", 1, 10m, "USD")]);
    }

    private static OrderAggregate CreateConfirmedOrder()
    {
        var order = CreateOrder();
        order.StartProcessing();
        order.Confirm();
        return order;
    }

    private static OrderItem CreateItem(
        string sku,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        return OrderItem.Create(
            Sku.From(sku),
            quantity,
            Money.From(unitPrice, currency));
    }
}
