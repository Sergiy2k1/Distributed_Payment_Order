using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class OrderIdTests
{
    [Fact]
    public void NewCreatesNonEmptyIdentifier()
    {
        var orderId = OrderId.New();

        Assert.NotEqual(Guid.Empty, orderId.Value);
    }

    [Fact]
    public void FromPreservesProvidedValue()
    {
        var value = Guid.NewGuid();

        var orderId = OrderId.From(value);

        Assert.Equal(value, orderId.Value);
    }

    [Fact]
    public void FromRejectsEmptyGuid()
    {
        var exception = Assert.Throws<ArgumentException>(() => OrderId.From(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void IdentifiersCreatedFromSameGuidAreEqual()
    {
        var value = Guid.NewGuid();

        var first = OrderId.From(value);
        var second = OrderId.From(value);

        Assert.Equal(first, second);
    }
}
