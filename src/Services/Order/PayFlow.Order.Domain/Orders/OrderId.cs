namespace PayFlow.Order.Domain.Orders;

public readonly record struct OrderId
{
    public Guid Value { get; }

    private OrderId(Guid value)
    {
        Value = value;
    }

    public static OrderId New() => From(Guid.NewGuid());

    public static OrderId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(value));
        }

        return new OrderId(value);
    }

    public override string ToString() => Value.ToString();
}
