using PayFlow.Inventory.Domain.Stock;

namespace PayFlow.Inventory.UnitTests.Stock;

public sealed class StockItemTests
{
    [Fact]
    public void ReserveReducesAvailableStockWithoutChangingOnHand()
    {
        var stock = StockItem.Create(
            "SKU-001",
            10);

        stock.Reserve(4);

        Assert.Equal(10, stock.OnHand);
        Assert.Equal(4, stock.Reserved);
        Assert.Equal(6, stock.Available);
        Assert.Equal(1, stock.Version);
    }

    [Fact]
    public void ReserveRejectsOversell()
    {
        var stock = StockItem.Create(
            "SKU-001",
            2);

        Assert.Throws<InvalidOperationException>(
            () => stock.Reserve(3));

        Assert.Equal(0, stock.Reserved);
    }

    [Fact]
    public void ReleaseReturnsReservedQuantityToAvailable()
    {
        var stock = StockItem.Create(
            "SKU-001",
            10);
        stock.Reserve(4);

        stock.Release(3);

        Assert.Equal(10, stock.OnHand);
        Assert.Equal(1, stock.Reserved);
        Assert.Equal(9, stock.Available);
    }

    [Fact]
    public void ConsumeReducesOnHandAndReservedTogether()
    {
        var stock = StockItem.Create(
            "SKU-001",
            10);
        stock.Reserve(4);

        stock.Consume(3);

        Assert.Equal(7, stock.OnHand);
        Assert.Equal(1, stock.Reserved);
        Assert.Equal(6, stock.Available);
    }

    [Fact]
    public void RestockIncreasesOnHand()
    {
        var stock = StockItem.Create(
            "SKU-001",
            10);

        stock.Restock(5);

        Assert.Equal(15, stock.OnHand);
        Assert.Equal(0, stock.Reserved);
        Assert.Equal(15, stock.Available);
    }
}
