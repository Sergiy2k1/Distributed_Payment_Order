using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class MoneyTests
{
    [Fact]
    public void FromPreservesAmountAndNormalizesCurrency()
    {
        var money = Money.From(12.34m, " usd ");

        Assert.Equal(12.34m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void ZeroCreatesZeroAmount()
    {
        var money = Money.Zero("EUR");

        Assert.Equal(0m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void FromRejectsNegativeAmount()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Money.From(-0.01m, "USD"));

        Assert.Equal("amount", exception.ParamName);
    }

    [Fact]
    public void FromRejectsMissingCurrency()
    {
        var exception = Assert.Throws<ArgumentException>(() => Money.From(10m, " "));

        Assert.Equal("currency", exception.ParamName);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("US1")]
    [InlineData("EURO")]
    public void FromRejectsInvalidCurrencyCode(string currency)
    {
        var exception = Assert.Throws<ArgumentException>(() => Money.From(10m, currency));

        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void AddSumsMoneyWithSameCurrency()
    {
        var left = Money.From(10.25m, "USD");
        var right = Money.From(4.75m, "usd");

        var result = left.Add(right);

        Assert.Equal(Money.From(15m, "USD"), result);
    }

    [Fact]
    public void AddRejectsDifferentCurrencies()
    {
        var usd = Money.From(10m, "USD");
        var eur = Money.From(10m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void SubtractReturnsDifferenceWithSameCurrency()
    {
        var total = Money.From(10m, "USD");
        var discount = Money.From(2.50m, "USD");

        var result = total.Subtract(discount);

        Assert.Equal(Money.From(7.50m, "USD"), result);
    }

    [Fact]
    public void SubtractRejectsDifferentCurrencies()
    {
        var usd = Money.From(10m, "USD");
        var eur = Money.From(2m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd.Subtract(eur));
    }

    [Fact]
    public void SubtractRejectsNegativeResult()
    {
        var total = Money.From(5m, "USD");
        var largerAmount = Money.From(6m, "USD");

        Assert.Throws<InvalidOperationException>(() => total.Subtract(largerAmount));
    }
}
