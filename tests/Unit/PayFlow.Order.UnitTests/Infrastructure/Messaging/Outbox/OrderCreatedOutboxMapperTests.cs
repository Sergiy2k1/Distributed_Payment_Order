using System.Text.Json;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Messaging.Outbox;

namespace PayFlow.Order.UnitTests.Infrastructure.Messaging.Outbox;

public sealed class OrderCreatedOutboxMapperTests
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 24, 20, 15, 0, TimeSpan.Zero);

    [Fact]
    public void MapCreatesVersionedOutboxEnvelope()
    {
        var orderId =
            OrderId.From(
                Guid.Parse("f9b98689-5a72-47d0-a47f-e3b614ca15b4"));
        var customerId =
            CustomerId.From(
                Guid.Parse("624c17e7-1734-48a9-a286-f8ca48ea2e95"));
        var domainEvent = new OrderCreatedDomainEvent(
            orderId,
            customerId,
            [
                OrderItem.Create(
                    Sku.From("SKU-001"),
                    2,
                    Money.From(10m, "USD")),
                OrderItem.Create(
                    Sku.From("SKU-002"),
                    3,
                    Money.From(5m, "USD"))
            ],
            Money.From(35m, "USD"),
            OccurredAtUtc);
        var outboxMessageId =
            Guid.Parse("9cbfced9-2e8a-4945-bf48-d543593d09de");
        var messageId =
            Guid.Parse("c7869c18-e09c-4e91-a4c7-e7aa23db4e1b");

        var actual = OrderCreatedOutboxMapper.Map(
            domainEvent,
            outboxMessageId,
            messageId,
            "00-test-trace");

        Assert.Equal(outboxMessageId, actual.OutboxMessageId);
        Assert.Equal(messageId, actual.MessageId);
        Assert.Equal("OrderCreated.v1", actual.MessageType);
        Assert.Equal(1, actual.SchemaVersion);
        Assert.Equal(orderId.Value, actual.AggregateId);
        Assert.Equal(orderId.Value, actual.CorrelationId);
        Assert.Null(actual.CausationId);
        Assert.Equal(OccurredAtUtc, actual.OccurredAtUtc);
        Assert.Equal("orders.events", actual.Destination);
        Assert.Equal("Order", actual.Producer);
        Assert.Equal("00-test-trace", actual.TraceParent);
        Assert.Equal(OccurredAtUtc, actual.CreatedAtUtc);
        Assert.Null(actual.PublishedAtUtc);
        Assert.Equal(0, actual.AttemptCount);
        Assert.Null(actual.NextAttemptAtUtc);
        Assert.Null(actual.LastErrorCode);

        using var payload = JsonDocument.Parse(actual.Payload);
        var root = payload.RootElement;

        Assert.Equal(
            orderId.Value,
            root.GetProperty("orderId").GetGuid());
        Assert.Equal(
            customerId.Value,
            root.GetProperty("customerId").GetGuid());
        Assert.Equal(
            "USD",
            root.GetProperty("currency").GetString());
        Assert.Equal(
            35m,
            root.GetProperty("totalAmount").GetDecimal());

        var items = root.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(
            "SKU-001",
            items[0].GetProperty("skuId").GetString());
        Assert.Equal(
            2,
            items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(
            10m,
            items[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(
            "SKU-002",
            items[1].GetProperty("skuId").GetString());
        Assert.Equal(
            3,
            items[1].GetProperty("quantity").GetInt32());
        Assert.Equal(
            5m,
            items[1].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public void MapProducesSamePayloadForSameDomainEvent()
    {
        var domainEvent = new OrderCreatedDomainEvent(
            OrderId.From(
                Guid.Parse("f9b98689-5a72-47d0-a47f-e3b614ca15b4")),
            CustomerId.From(
                Guid.Parse("624c17e7-1734-48a9-a286-f8ca48ea2e95")),
            [
                OrderItem.Create(
                    Sku.From("SKU-001"),
                    1,
                    Money.From(10m, "USD"))
            ],
            Money.From(10m, "USD"),
            OccurredAtUtc);

        var first = OrderCreatedOutboxMapper.Map(
            domainEvent,
            Guid.NewGuid(),
            Guid.NewGuid());

        var second = OrderCreatedOutboxMapper.Map(
            domainEvent,
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(first.Payload, second.Payload);
    }
}
