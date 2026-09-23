using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Order.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("orders_db")
            .WithUsername("payflow_order")
            .WithPassword("payflow-test")
            .Build();

    public OrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new OrderDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _container
            .StartAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var dbContext = CreateDbContext();

        await dbContext.Database
            .MigrateAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _container
            .DisposeAsync()
            .ConfigureAwait(false);
    }
}
