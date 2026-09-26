namespace PayFlow.Inventory.Domain.Reservations;

public sealed record InventoryReservationItem
{
    private InventoryReservationItem(
        string skuId,
        int quantity)
    {
        SkuId = skuId;
        Quantity = quantity;
    }

    public string SkuId { get; }

    public int Quantity { get; }

    public static InventoryReservationItem Create(
        string skuId,
        int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skuId);

        if (skuId.Length > 128)
        {
            throw new ArgumentException(
                "SKU ID cannot exceed 128 characters.",
                nameof(skuId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Reservation quantity must be greater than zero.");
        }

        return new InventoryReservationItem(
            skuId.Trim(),
            quantity);
    }
}
