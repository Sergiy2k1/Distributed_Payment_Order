namespace PayFlow.Order.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem(Sku sku, int quantity, Money unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Sku Sku { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal => Money.From(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    public static OrderItem Create(Sku sku, int quantity, Money unitPrice)
    {
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Order item quantity must be greater than zero.");
        }

        if (unitPrice.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                unitPrice.Amount,
                "Order item unit price must be greater than zero.");
        }

        return new OrderItem(sku, quantity, unitPrice);
    }
}
