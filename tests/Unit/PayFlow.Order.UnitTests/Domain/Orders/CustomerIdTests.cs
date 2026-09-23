using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class CustomerIdTests
{
    [Fact]
    public void NewCreatesNonEmptyIdentifier()
    {
        var customerId = CustomerId.New();

        Assert.NotEqual(Guid.Empty, customerId.Value);
    }

    [Fact]
    public void FromPreservesProvidedValue()
    {
        var value = Guid.NewGuid();

        var customerId = CustomerId.From(value);

        Assert.Equal(value, customerId.Value);
    }

    [Fact]
    public void FromRejectsEmptyGuid()
    {
        var exception = Assert.Throws<ArgumentException>(() => CustomerId.From(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void IdentifiersCreatedFromSameGuidAreEqual()
    {
        var value = Guid.NewGuid();

        var first = CustomerId.From(value);
        var second = CustomerId.From(value);

        Assert.Equal(first, second);
    }
}
