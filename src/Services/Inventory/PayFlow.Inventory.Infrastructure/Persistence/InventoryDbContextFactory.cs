using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayFlow.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContextFactory
    : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(
        string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "PAYFLOW_INVENTORY_DB")
            ?? "Host=localhost;Port=5435;Database=inventory_db;Username=payflow_inventory;Password=payflow_inventory";

        var options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new InventoryDbContext(options);
    }
}
