using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContext
    : DbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryReservationEntity> Reservations =>
        Set<InventoryReservationEntity>();

    public DbSet<InventoryReservationItemEntity> ReservationItems =>
        Set<InventoryReservationItemEntity>();

    public DbSet<StockItemEntity> StockItems =>
        Set<StockItemEntity>();

    public DbSet<InboxMessageEntity> InboxMessages =>
        Set<InboxMessageEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages =>
        Set<OutboxMessageEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InventoryDbContext).Assembly);
    }
}
