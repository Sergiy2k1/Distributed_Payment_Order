namespace PayFlow.Inventory.Infrastructure.Persistence.Entities;

public sealed class StockItemEntity
{
    public string SkuId { get; set; } = string.Empty;

    public int OnHand { get; set; }

    public int Reserved { get; set; }

    public long Version { get; set; }
}
