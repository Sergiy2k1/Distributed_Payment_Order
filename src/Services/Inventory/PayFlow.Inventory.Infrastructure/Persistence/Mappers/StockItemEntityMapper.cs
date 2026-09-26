using PayFlow.Inventory.Domain.Stock;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Mappers;

public static class StockItemEntityMapper
{
    public static StockItem ToDomain(
        StockItemEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return StockItem.Rehydrate(
            entity.SkuId,
            entity.OnHand,
            entity.Reserved,
            entity.Version);
    }

    public static void Apply(
        StockItem stock,
        StockItemEntity entity)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(entity);

        if (!string.Equals(
                stock.SkuId,
                entity.SkuId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Cannot apply stock state to a different SKU row.");
        }

        entity.OnHand = stock.OnHand;
        entity.Reserved = stock.Reserved;
        entity.Version = stock.Version;
    }
}
