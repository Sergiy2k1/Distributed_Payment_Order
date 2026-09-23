using PayFlow.Order.Domain.Orders;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class OrderTests
{
    private static readonly DateTimeOffset InitialTimestamp =
        new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateInitializesPendingOrder()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var item = CreateItem("SKU-001", 1, 10m, "USD");

        var order = OrderAggregate.Create(
            orderId,
            customerId,
            [item],
            InitialTimestamp);

        Assert.Equal(orderId, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(order.Items);
        Assert.Same(item, order.Items[0]);
        Assert.Equal(Money.From(10m, "USD"), order.Total);
        Assert.Equal(InitialTimestamp, order.CreatedAtUtc);
        Assert.Equal(InitialTimestamp, order.UpdatedAtUtc);
        Assert.Equal(0, order.Version);
    }

    [Fact]
    public void CreateRejectsNonUtcTimestamp()
    {
        var timestamp = new DateTimeOffset(
            2026,
            9,
            23,
            12,
            0,
            0,
            TimeSpan.FromHours(3));

        var exception = Assert.Throws<ArgumentException>(
            () => OrderAggregate.Create(
                OrderId.New(),
                CustomerId.New(),
                [CreateItem("SKU-001", 1, 10m, "USD")],
                timestamp));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsEmptyItems()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => OrderAggregate.Create(
                OrderId.New(),
                CustomerId.New(),
                [],
                InitialTimestamp));

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
                items,
                InitialTimestamp));

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
            items,
            InitialTimestamp);

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
            items,
            InitialTimestamp);

        items.Add(CreateItem("SKU-002", 1, 20m, "USD"));

        Assert.Single(order.Items);
        Assert.Equal(Money.From(10m, "USD"), order.Total);
    }

    [Fact]
    public void CheckoutHappyPathReachesConfirmed()
    {
        var order = CreateOrder();

        order.StartProcessing(InitialTimestamp.AddMinutes(1));
        order.Confirm(InitialTimestamp.AddMinutes(2));

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void PendingOrderCanBeCancelled()
    {
        var order = CreateOrder();

        order.BeginCancellation(InitialTimestamp.AddMinutes(1));
        order.CompleteCancellation(InitialTimestamp.AddMinutes(2));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ProcessingOrderCanBeCancelled()
    {
        var order = CreateOrder();
        order.StartProcessing(InitialTimestamp.AddMinutes(1));

        order.BeginCancellation(InitialTimestamp.AddMinutes(2));
        order.CompleteCancellation(InitialTimestamp.AddMinutes(3));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ConfirmedOrderCanBeRefunded()
    {
        var order = CreateConfirmedOrder();

        order.RequestRefund(InitialTimestamp.AddMinutes(3));
        order.CompleteRefund(InitialTimestamp.AddMinutes(4));

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void ConfirmedOrderCanBeFulfilled()
    {
        var order = CreateConfirmedOrder();

        order.Fulfill(InitialTimestamp.AddMinutes(3));

        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void TransitionUpdatesTimestampAndVersion()
    {
        var order = CreateOrder();
        var transitionTimestamp = InitialTimestamp.AddMinutes(1);

        order.StartProcessing(transitionTimestamp);

        Assert.Equal(transitionTimestamp, order.UpdatedAtUtc);
        Assert.Equal(1, order.Version);
        Assert.Equal(InitialTimestamp, order.CreatedAtUtc);
    }

    [Fact]
    public void RepeatingSameTransitionIsIdempotent()
    {
        var order = CreateOrder();
        var firstTimestamp = InitialTimestamp.AddMinutes(1);

        order.StartProcessing(firstTimestamp);
        order.StartProcessing(InitialTimestamp.AddMinutes(2));

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(firstTimestamp, order.UpdatedAtUtc);
        Assert.Equal(1, order.Version);
    }

    [Fact]
    public void TransitionRejectsEarlierTimestamp()
    {
        var order = CreateOrder();
        order.StartProcessing(InitialTimestamp.AddMinutes(2));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => order.Confirm(InitialTimestamp.AddMinutes(1)));

        Assert.Equal("occurredAtUtc", exception.ParamName);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(1, order.Version);
    }

    [Fact]
    public void ConfirmFromPendingIsRejected()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<InvalidOperationException>(
            () => order.Confirm(InitialTimestamp.AddMinutes(1)));

        Assert.Equal("Order cannot transition from Pending to Confirmed.", exception.Message);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, order.Version);
    }

    [Fact]
    public void CancelledOrderRejectsFurtherBusinessTransition()
    {
        var order = CreateOrder();
        order.BeginCancellation(InitialTimestamp.AddMinutes(1));
        order.CompleteCancellation(InitialTimestamp.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(
            () => order.StartProcessing(InitialTimestamp.AddMinutes(3)));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void RefundedOrderRejectsFulfillment()
    {
        var order = CreateConfirmedOrder();
        order.RequestRefund(InitialTimestamp.AddMinutes(3));
        order.CompleteRefund(InitialTimestamp.AddMinutes(4));

        Assert.Throws<InvalidOperationException>(
            () => order.Fulfill(InitialTimestamp.AddMinutes(5)));
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    private static OrderAggregate CreateOrder()
    {
        return OrderAggregate.Create(
            OrderId.New(),
            CustomerId.New(),
            [CreateItem("SKU-001", 1, 10m, "USD")],
            InitialTimestamp);
    }

    private static OrderAggregate CreateConfirmedOrder()
    {
        var order = CreateOrder();
        order.StartProcessing(InitialTimestamp.AddMinutes(1));
        order.Confirm(InitialTimestamp.AddMinutes(2));
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
