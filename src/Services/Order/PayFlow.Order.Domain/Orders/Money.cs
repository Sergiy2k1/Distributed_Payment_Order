namespace PayFlow.Order.Domain.Orders;

public sealed record Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency) => From(0m, currency);

    public static Money From(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money amount cannot be negative.");
        }

        return new Money(amount, NormalizeCurrency(currency));
    }

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);

        if (other.Amount > Amount)
        {
            throw new InvalidOperationException("Money subtraction cannot produce a negative amount.");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || normalized.Any(static character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must be a three-letter alphabetic code.", nameof(currency));
        }

        return normalized;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Money values must use the same currency.");
        }
    }
}
