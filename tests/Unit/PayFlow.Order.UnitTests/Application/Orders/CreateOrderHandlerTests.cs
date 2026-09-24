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
    public async Task HandleCreatesPersistsAndCommitsPendingOrder()
    {
        var repository = new FakeOrderRepository();
        var idempotencyRepository =
            new FakeCreateOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            repository,
            idempotencyRepository,
            unitOfWork);
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

        var order = Assert.IsType<OrderAggregate>(
            repository.AddedOrder);
        Assert.Equal(result.OrderId, order.Id.Value);
        Assert.Equal(customerId, order.CustomerId.Value);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(FixedUtcNow, order.CreatedAtUtc);
        Assert.Equal(Money.From(35m, "USD"), order.Total);
        Assert.Equal(35m, result.TotalAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Null(idempotencyRepository.AddedRecord);
        Assert.IsType<OrderCreatedDomainEvent>(
            Assert.Single(order.DomainEvents));
    }

    [Fact]
    public async Task HandlePassesCancellationTokenToPersistenceBoundary()
    {
        var repository = new FakeOrderRepository();
        var idempotencyRepository =
            new FakeCreateOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            repository,
            idempotencyRepository,
            unitOfWork);
        using var cancellationTokenSource =
            new CancellationTokenSource();
        var command = CreateValidCommand();

        await handler.HandleAsync(
            command,
            "create-order-token",
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            repository.ReceivedCancellationToken);
        Assert.Equal(
            cancellationTokenSource.Token,
            idempotencyRepository.ReceivedGetCancellationToken);
        Assert.Equal(
            cancellationTokenSource.Token,
            idempotencyRepository.ReceivedAddCancellationToken);
        Assert.Equal(
            cancellationTokenSource.Token,
            unitOfWork.ReceivedCancellationToken);
    }

    [Fact]
    public async Task HandleWithNewIdempotencyKeyPersistsRecordAndOrderTogether()
    {
        var repository = new FakeOrderRepository();
        var idempotencyRepository =
            new FakeCreateOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            repository,
            idempotencyRepository,
            unitOfWork);
        var command = CreateValidCommand();

        var result = await handler.HandleAsync(
            command,
            "create-order-001",
            TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedOrder);
        var record = Assert.IsType<CreateOrderIdempotencyRecord>(
            idempotencyRepository.AddedRecord);
        Assert.Equal("create-order-001", record.IdempotencyKey);
        Assert.Equal(
            CreateOrderRequestHasher.ComputeHash(command),
            record.RequestHash);
        Assert.Equal(result, record.Result);
        Assert.Equal(FixedUtcNow, record.CreatedAtUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleWithSameKeyAndPayloadReturnsOriginalResult()
    {
        var command = CreateValidCommand();
        var originalResult = new CreateOrderResult(
            Guid.NewGuid(),
            OrderStatus.Pending,
            10m,
            "USD");
        var idempotencyRepository =
            new FakeCreateOrderIdempotencyRepository
            {
                ExistingRecord = new CreateOrderIdempotencyRecord(
                    "create-order-001",
                    CreateOrderRequestHasher.ComputeHash(command),
                    originalResult,
                    FixedUtcNow)
            };
        var repository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            repository,
            idempotencyRepository,
            unitOfWork);

        var result = await handler.HandleAsync(
            command,
            "create-order-001",
            TestContext.Current.CancellationToken);

        Assert.Equal(originalResult, result);
        Assert.Null(repository.AddedOrder);
        Assert.Null(idempotencyRepository.AddedRecord);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleWithSameKeyAndDifferentPayloadThrowsConflict()
    {
        var command = CreateValidCommand();
        var idempotencyRepository =
            new FakeCreateOrderIdempotencyRepository
            {
                ExistingRecord = new CreateOrderIdempotencyRecord(
                    "create-order-001",
                    new string('A', 64),
                    new CreateOrderResult(
                        Guid.NewGuid(),
                        OrderStatus.Pending,
                        10m,
                        "USD"),
                    FixedUtcNow)
            };
        var repository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            repository,
            idempotencyRepository,
            unitOfWork);

        await Assert.ThrowsAsync<
            CreateOrderIdempotencyConflictException>(
            () => handler.HandleAsync(
                command,
                "create-order-001",
                TestContext.Current.CancellationToken));

        Assert.Null(repository.AddedOrder);
        Assert.Null(idempotencyRepository.AddedRecord);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleRejectsEmptyCustomerId()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeOrderRepository(),
            new FakeCreateOrderIdempotencyRepository(),
            unitOfWork);
        var command = new CreateOrderCommand(
            Guid.Empty,
            [new CreateOrderItem("SKU-001", 1, 10m, "USD")]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken));

        Assert.Equal("value", exception.ParamName);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleRejectsEmptyItems()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeOrderRepository(),
            new FakeCreateOrderIdempotencyRepository(),
            unitOfWork);
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            []);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken));

        Assert.Equal("items", exception.ParamName);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    private static CreateOrderHandler CreateHandler(
        IOrderRepository orderRepository,
        ICreateOrderIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateOrderHandler(
            orderRepository,
            idempotencyRepository,
            unitOfWork,
            new FakeClock(FixedUtcNow));
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

    private sealed class FakeCreateOrderIdempotencyRepository
        : ICreateOrderIdempotencyRepository
    {
        public CreateOrderIdempotencyRecord? ExistingRecord { get; init; }

        public CreateOrderIdempotencyRecord? AddedRecord { get; private set; }

        public CancellationToken ReceivedGetCancellationToken { get; private set; }

        public CancellationToken ReceivedAddCancellationToken { get; private set; }

        public Task<CreateOrderIdempotencyRecord?> GetByKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            ReceivedGetCancellationToken = cancellationToken;
            return Task.FromResult(ExistingRecord);
        }

        public Task AddAsync(
            CreateOrderIdempotencyRecord record,
            CancellationToken cancellationToken = default)
        {
            AddedRecord = record;
            ReceivedAddCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
