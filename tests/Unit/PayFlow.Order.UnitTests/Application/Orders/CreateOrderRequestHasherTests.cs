using PayFlow.Order.Application.Orders.CreateOrder;

namespace PayFlow.Order.UnitTests.Application.Orders;

public sealed class CreateOrderRequestHasherTests
{
    [Fact]
    public void ComputeHashNormalizesEquivalentRequestValues()
    {
        var customerId =
            Guid.Parse("9fd61a0a-e94f-45f4-9e8c-533696f8b79e");

        var first = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    " SKU-001 ",
                    2,
                    10.00m,
                    " usd ")
            ]);

        var second = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-001",
                    2,
                    10m,
                    "USD")
            ]);

        var firstHash =
            CreateOrderRequestHasher.ComputeHash(first);

        var secondHash =
            CreateOrderRequestHasher.ComputeHash(second);

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void ComputeHashChangesWhenBusinessPayloadChanges()
    {
        var customerId =
            Guid.Parse("9fd61a0a-e94f-45f4-9e8c-533696f8b79e");

        var first = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-001",
                    1,
                    10m,
                    "USD")
            ]);

        var second = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-001",
                    2,
                    10m,
                    "USD")
            ]);

        Assert.NotEqual(
            CreateOrderRequestHasher.ComputeHash(first),
            CreateOrderRequestHasher.ComputeHash(second));
    }

    [Fact]
    public void ComputeHashPreservesItemOrder()
    {
        var customerId =
            Guid.Parse("9fd61a0a-e94f-45f4-9e8c-533696f8b79e");

        var first = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-001",
                    1,
                    10m,
                    "USD"),
                new CreateOrderItem(
                    "SKU-002",
                    1,
                    5m,
                    "USD")
            ]);

        var second = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-002",
                    1,
                    5m,
                    "USD"),
                new CreateOrderItem(
                    "SKU-001",
                    1,
                    10m,
                    "USD")
            ]);

        Assert.NotEqual(
            CreateOrderRequestHasher.ComputeHash(first),
            CreateOrderRequestHasher.ComputeHash(second));
    }
}
