using PayFlow.Inventory.Domain.Stock;

namespace PayFlow.Inventory.Application.Abstractions;

public interface IStockRepository
{
    Task AddAsync(
        StockItem stock,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockItem>> LockBySkuIdsAsync(
        IReadOnlyCollection<string> skuIds,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        IReadOnlyCollection<StockItem> stockItems,
        CancellationToken cancellationToken = default);
}
