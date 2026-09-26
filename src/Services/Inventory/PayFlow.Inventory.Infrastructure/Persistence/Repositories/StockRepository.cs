using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Stock;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;
using PayFlow.Inventory.Infrastructure.Persistence.Mappers;

namespace PayFlow.Inventory.Infrastructure.Persistence.Repositories;

public sealed class StockRepository
    : IStockRepository
{
    private readonly InventoryDbContext _dbContext;

    public StockRepository(
        InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        StockItem stock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stock);

        await _dbContext.StockItems
            .AddAsync(
                new StockItemEntity
                {
                    SkuId = stock.SkuId,
                    OnHand = stock.OnHand,
                    Reserved = stock.Reserved,
                    Version = stock.Version
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StockItem>> LockBySkuIdsAsync(
        IReadOnlyCollection<string> skuIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skuIds);

        var canonicalSkuIds = skuIds
            .Select(
                skuId =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(
                        skuId);

                    return skuId.Trim();
                })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                static skuId => skuId,
                StringComparer.Ordinal)
            .ToArray();

        if (canonicalSkuIds.Length == 0)
        {
            throw new ArgumentException(
                "At least one SKU is required.",
                nameof(skuIds));
        }

        if (_dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Stock rows may only be locked inside an explicit database transaction.");
        }

        var entities = await _dbContext.StockItems
            .FromSqlInterpolated(
                $"""
                 SELECT *
                 FROM inventory_stock
                 WHERE sku_id = ANY ({canonicalSkuIds})
                 ORDER BY sku_id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(StockItemEntityMapper.ToDomain)
            .ToArray();
    }

    public Task ApplyAsync(
        IReadOnlyCollection<StockItem> stockItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stockItems);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var stock in stockItems)
        {
            ArgumentNullException.ThrowIfNull(stock);

            var entity = _dbContext.StockItems.Local
                .SingleOrDefault(
                    tracked =>
                        string.Equals(
                            tracked.SkuId,
                            stock.SkuId,
                            StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Stock row '{stock.SkuId}' must be locked by this repository before applying a mutation.");

            StockItemEntityMapper.Apply(
                stock,
                entity);
        }

        return Task.CompletedTask;
    }
}
