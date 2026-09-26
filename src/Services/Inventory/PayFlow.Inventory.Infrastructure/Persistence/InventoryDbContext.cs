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

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InventoryDbContext).Assembly);
    }
}
