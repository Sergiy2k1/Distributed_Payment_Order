using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class OrderIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var orderId = OrderId.New();

        Assert.NotEqual(Guid.Empty, orderId.Value);
    }

    [Fact]
    public void From_ShouldPreserveProvidedValue()
    {
        var value = Guid.NewGuid();

        var orderId = OrderId.From(value);

        Assert.Equal(value, orderId.Value);
    }

    [Fact]
    public void From_ShouldRejectEmptyGuid()
    {
        var exception = Assert.Throws<ArgumentException>(() => OrderId.From(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void IdentifiersCreatedFromSameGuid_ShouldBeEqual()
    {
        var value = Guid.NewGuid();

        var first = OrderId.From(value);
        var second = OrderId.From(value);

        Assert.Equal(first, second);
    }
}
