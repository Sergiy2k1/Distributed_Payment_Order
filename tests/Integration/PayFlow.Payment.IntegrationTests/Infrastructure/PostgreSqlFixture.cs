using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Payment.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payments_db")
            .WithUsername("payflow_payment")
            .WithPassword("payflow-test")
            .Build();

    public PaymentDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

        return new PaymentDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await _container.StartAsync(cancellationToken);

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
