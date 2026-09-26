namespace PayFlow.Saga.Domain.Checkout;

public sealed record CheckoutSagaItem
{
    private CheckoutSagaItem(
        string skuId,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        SkuId = skuId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Currency = currency;
    }

    public string SkuId { get; }

    public int Quantity { get; }

    public decimal UnitPrice { get; }

    public string Currency { get; }

    public decimal LineTotal =>
        UnitPrice * Quantity;

    public static CheckoutSagaItem Create(
        string skuId,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            skuId);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Quantity must be greater than zero.");
        }

        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                unitPrice,
                "Unit price must be greater than zero.");
        }

        var normalizedCurrency =
            NormalizeCurrency(currency);

        return new CheckoutSagaItem(
            skuId.Trim(),
            quantity,
            unitPrice,
            normalizedCurrency);
    }

    internal static string NormalizeCurrency(
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            currency);

        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3
            || normalized.Any(
                character =>
                    character is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                "Currency must contain exactly three ASCII letters.",
                nameof(currency));
        }

        return normalized;
    }
}
