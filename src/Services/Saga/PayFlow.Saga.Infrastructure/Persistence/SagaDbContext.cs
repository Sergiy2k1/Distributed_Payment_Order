using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence;

public sealed class SagaDbContext : DbContext
{
    public SagaDbContext(
        DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    public DbSet<InboxMessageEntity> InboxMessages =>
        Set<InboxMessageEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SagaDbContext).Assembly);
    }
}
