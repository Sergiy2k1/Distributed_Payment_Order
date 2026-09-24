using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.IntegrationTests.Infrastructure;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.IntegrationTests.Persistence;

public sealed class CreateOrderIdempotencyRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 24, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAndGetByKeyPersistOriginalCreateResult()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var order = OrderAggregate.Create(
            orderId,
            customerId,
            [
                OrderItem.Create(
                    Sku.From("SKU-001"),
                    2,
                    Money.From(10m, "USD"))
            ],
            CreatedAtUtc);

        var expected = new CreateOrderIdempotencyRecord(
            "create-order-001",
            new string('A', 64),
            new CreateOrderResult(
                orderId.Value,
                OrderStatus.Pending,
                20m,
                "USD"),
            CreatedAtUtc);

        await using (var writeDbContext =
            fixture.CreateDbContext())
        {
            var orderRepository =
                new OrderRepository(writeDbContext);
            var idempotencyRepository =
                new CreateOrderIdempotencyRepository(
                    writeDbContext);
            var unitOfWork =
                new EfUnitOfWork(writeDbContext);

            await orderRepository.AddAsync(
                order,
                cancellationToken);

            await idempotencyRepository.AddAsync(
                expected,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        await using var readDbContext =
            fixture.CreateDbContext();
        var readRepository =
            new CreateOrderIdempotencyRepository(
                readDbContext);

        var actual = await readRepository.GetByKeyAsync(
            expected.IdempotencyKey,
            cancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetByKeyReturnsNullWhenRecordDoesNotExist()
    {
        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new CreateOrderIdempotencyRepository(dbContext);

        var actual = await repository.GetByKeyAsync(
            "missing-key",
            TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }
}
