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

    public DbSet<CheckoutSagaEntity> CheckoutSagas =>
        Set<CheckoutSagaEntity>();

    public DbSet<CheckoutSagaItemEntity> CheckoutSagaItems =>
        Set<CheckoutSagaItemEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages =>
        Set<OutboxMessageEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SagaDbContext).Assembly);
    }
}
