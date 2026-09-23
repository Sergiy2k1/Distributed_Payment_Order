namespace PayFlow.Order.Domain.Orders;

public readonly record struct CustomerId
{
    public Guid Value { get; }

    private CustomerId(Guid value)
    {
        Value = value;
    }

    public static CustomerId New() => From(Guid.NewGuid());

    public static CustomerId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(value));
        }

        return new CustomerId(value);
    }

    public override string ToString() => Value.ToString();
}
