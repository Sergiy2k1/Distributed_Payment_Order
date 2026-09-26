using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Inventory.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("inventory_db")
            .WithUsername("payflow_inventory")
            .WithPassword("payflow-test")
            .Build();

    public InventoryDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseNpgsql(
                    _container.GetConnectionString())
                .Options;

        return new InventoryDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await _container
            .StartAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var dbContext =
            CreateDbContext();

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
