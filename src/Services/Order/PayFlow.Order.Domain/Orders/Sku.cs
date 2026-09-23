namespace PayFlow.Order.Domain.Orders;

public sealed record Sku
{
    public string Value { get; }

    private Sku(string value)
    {
        Value = value;
    }

    public static Sku From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SKU is required.", nameof(value));
        }

        return new Sku(value.Trim());
    }

    public override string ToString() => Value;
}
