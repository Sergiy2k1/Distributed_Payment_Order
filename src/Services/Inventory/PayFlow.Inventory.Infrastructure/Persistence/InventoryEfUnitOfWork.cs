using PayFlow.Inventory.Application.Abstractions;

namespace PayFlow.Inventory.Infrastructure.Persistence;

public sealed class InventoryEfUnitOfWork
    : IInventoryUnitOfWork
{
    private readonly InventoryDbContext _dbContext;

    public InventoryEfUnitOfWork(
        InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
