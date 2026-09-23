using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.IntegrationTests.Infrastructure;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.IntegrationTests.Persistence;

public sealed class OrderRepositoryTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 23, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsyncPersistsOrderAndItems()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var order = OrderAggregate.Create(
            orderId,
            customerId,
            [
                OrderItem.Create(
                    Sku.From("SKU-001"),
                    2,
                    Money.From(10m, "USD")),
                OrderItem.Create(
                    Sku.From("SKU-002"),
                    3,
                    Money.From(5m, "USD"))
            ],
            CreatedAtUtc);

        await using (var writeDbContext = fixture.CreateDbContext())
        {
            var repository = new OrderRepository(writeDbContext);
            var unitOfWork = new EfUnitOfWork(writeDbContext);

            await repository
                .AddAsync(order, cancellationToken)
                .ConfigureAwait(false);

            await unitOfWork
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await using var readDbContext = fixture.CreateDbContext();

        var persistedOrder = await readDbContext.Orders
            .AsNoTracking()
            .Include(entity => entity.Items)
            .SingleAsync(
                entity => entity.Id == orderId.Value,
                cancellationToken)
            .ConfigureAwait(false);

        Assert.Equal(orderId.Value, persistedOrder.Id);
        Assert.Equal(customerId.Value, persistedOrder.CustomerId);
        Assert.Equal("Pending", persistedOrder.Status);
        Assert.Equal(35m, persistedOrder.TotalAmount);
        Assert.Equal("USD", persistedOrder.Currency);
        Assert.Equal(CreatedAtUtc, persistedOrder.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, persistedOrder.UpdatedAtUtc);
        Assert.Equal(0, persistedOrder.Version);

        var persistedItems = persistedOrder.Items
            .OrderBy(item => item.Position)
            .ToArray();

        Assert.Collection(
            persistedItems,
            item =>
            {
                Assert.Equal(0, item.Position);
                Assert.Equal("SKU-001", item.Sku);
                Assert.Equal(2, item.Quantity);
                Assert.Equal(10m, item.UnitPriceAmount);
                Assert.Equal("USD", item.Currency);
            },
            item =>
            {
                Assert.Equal(1, item.Position);
                Assert.Equal("SKU-002", item.Sku);
                Assert.Equal(3, item.Quantity);
                Assert.Equal(5m, item.UnitPriceAmount);
                Assert.Equal("USD", item.Currency);
            });
    }
}
