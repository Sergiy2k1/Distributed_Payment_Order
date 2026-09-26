using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Saga.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("saga_db")
            .WithUsername("payflow_saga")
            .WithPassword("payflow-test")
            .Build();

    public SagaDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<SagaDbContext>()
                .UseNpgsql(
                    _container.GetConnectionString())
                .Options;

        return new SagaDbContext(options);
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
