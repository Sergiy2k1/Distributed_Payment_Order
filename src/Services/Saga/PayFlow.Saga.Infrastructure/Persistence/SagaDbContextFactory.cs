using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayFlow.Saga.Infrastructure.Persistence;

public sealed class SagaDbContextFactory
    : IDesignTimeDbContextFactory<SagaDbContext>
{
    public SagaDbContext CreateDbContext(
        string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__SagaDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Environment variable 'ConnectionStrings__SagaDatabase' is not configured.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<SagaDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new SagaDbContext(
            optionsBuilder.Options);
    }
}
