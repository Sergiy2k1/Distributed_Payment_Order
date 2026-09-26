namespace PayFlow.Inventory.Domain.Stock;

public sealed class StockItem
{
    private StockItem(
        string skuId,
        int onHand,
        int reserved,
        long version)
    {
        SkuId = skuId;
        OnHand = onHand;
        Reserved = reserved;
        Version = version;
    }

    public string SkuId { get; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int Available => OnHand - Reserved;

    public long Version { get; private set; }

    public static StockItem Create(
        string skuId,
        int onHand)
    {
        return Rehydrate(
            skuId,
            onHand,
            reserved: 0,
            version: 0);
    }

    public static StockItem Rehydrate(
        string skuId,
        int onHand,
        int reserved,
        long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skuId);

        if (skuId.Length > 128)
        {
            throw new ArgumentException(
                "SKU ID cannot exceed 128 characters.",
                nameof(skuId));
        }

        if (onHand < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(onHand),
                onHand,
                "On-hand stock cannot be negative.");
        }

        if (reserved < 0 || reserved > onHand)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reserved),
                reserved,
                "Reserved stock must be between zero and on-hand quantity.");
        }

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Stock version cannot be negative.");
        }

        return new StockItem(
            skuId.Trim(),
            onHand,
            reserved,
            version);
    }

    public void Reserve(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        if (Available < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient available stock for SKU '{SkuId}'.");
        }

        Reserved += quantity;
        Version++;
    }

    public void Release(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        if (Reserved < quantity)
        {
            throw new InvalidOperationException(
                $"Cannot release more reserved stock than exists for SKU '{SkuId}'.");
        }

        Reserved -= quantity;
        Version++;
    }

    public void Consume(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        if (Reserved < quantity)
        {
            throw new InvalidOperationException(
                $"Cannot consume more reserved stock than exists for SKU '{SkuId}'.");
        }

        Reserved -= quantity;
        OnHand -= quantity;
        Version++;
    }

    public void Restock(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        checked
        {
            OnHand += quantity;
        }

        Version++;
    }

    private static void EnsurePositiveQuantity(
        int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Stock quantity must be greater than zero.");
        }
    }
}
