using PayFlow.Order.Domain.Orders;

namespace PayFlow.Order.UnitTests.Domain.Orders;

public sealed class OrderItemTests
{
    [Fact]
    public void SkuFromTrimsValue()
    {
        var sku = Sku.From("  SKU-001  ");

        Assert.Equal("SKU-001", sku.Value);
    }

    [Fact]
    public void SkuFromRejectsMissingValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Sku.From(" "));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void CreatePreservesItemValues()
    {
        var sku = Sku.From("SKU-001");
        var unitPrice = Money.From(12.50m, "USD");

        var item = OrderItem.Create(sku, 3, unitPrice);

        Assert.Equal(sku, item.Sku);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(unitPrice, item.UnitPrice);
    }

    [Fact]
    public void CreateCalculatesLineTotal()
    {
        var item = OrderItem.Create(
            Sku.From("SKU-001"),
            4,
            Money.From(2.50m, "USD"));

        Assert.Equal(Money.From(10m, "USD"), item.LineTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRejectsNonPositiveQuantity(int quantity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => OrderItem.Create(
                Sku.From("SKU-001"),
                quantity,
                Money.From(10m, "USD")));

        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsZeroUnitPrice()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => OrderItem.Create(
                Sku.From("SKU-001"),
                1,
                Money.Zero("USD")));

        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsNullSku()
    {
        Assert.Throws<ArgumentNullException>(
            () => OrderItem.Create(null!, 1, Money.From(10m, "USD")));
    }

    [Fact]
    public void CreateRejectsNullUnitPrice()
    {
        Assert.Throws<ArgumentNullException>(
            () => OrderItem.Create(Sku.From("SKU-001"), 1, null!));
    }
}
