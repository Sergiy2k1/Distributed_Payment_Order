using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.UnitTests.Application.Orders;

public sealed class CreateOrderHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 9, 23, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleCreatesAndPersistsPendingOrder()
    {
        var repository = new FakeOrderRepository();
        var handler = new CreateOrderHandler(
            repository,
            new FakeClock(FixedUtcNow));
        var customerId = Guid.NewGuid();
        var command = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem("SKU-001", 2, 10m, "USD"),
                new CreateOrderItem("SKU-002", 3, 5m, "usd")
            ]);

        var result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        var order = Assert.IsType<OrderAggregate>(repository.AddedOrder);
        Assert.Equal(result.OrderId, order.Id.Value);
        Assert.Equal(customerId, order.CustomerId.Value);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(FixedUtcNow, order.CreatedAtUtc);
        Assert.Equal(Money.From(35m, "USD"), order.Total);
        Assert.Equal(35m, result.TotalAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.IsType<OrderCreatedDomainEvent>(
            Assert.Single(order.DomainEvents));
    }

    [Fact]
    public async Task HandlePassesCancellationTokenToRepository()
    {
        var repository = new FakeOrderRepository();
        var handler = new CreateOrderHandler(
            repository,
            new FakeClock(FixedUtcNow));
        using var cancellationTokenSource = new CancellationTokenSource();
        var command = CreateValidCommand();

        await handler.HandleAsync(
            command,
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            repository.ReceivedCancellationToken);
    }

    [Fact]
    public async Task HandleRejectsEmptyCustomerId()
    {
        var handler = new CreateOrderHandler(
            new FakeOrderRepository(),
            new FakeClock(FixedUtcNow));
        var command = new CreateOrderCommand(
            Guid.Empty,
            [new CreateOrderItem("SKU-001", 1, 10m, "USD")]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public async Task HandleRejectsEmptyItems()
    {
        var handler = new CreateOrderHandler(
            new FakeOrderRepository(),
            new FakeClock(FixedUtcNow));
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            []);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken));

        Assert.Equal("items", exception.ParamName);
    }

    private static CreateOrderCommand CreateValidCommand()
    {
        return new CreateOrderCommand(
            Guid.NewGuid(),
            [new CreateOrderItem("SKU-001", 1, 10m, "USD")]);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public OrderAggregate? AddedOrder { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task AddAsync(
            OrderAggregate order,
            CancellationToken cancellationToken = default)
        {
            AddedOrder = order;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
