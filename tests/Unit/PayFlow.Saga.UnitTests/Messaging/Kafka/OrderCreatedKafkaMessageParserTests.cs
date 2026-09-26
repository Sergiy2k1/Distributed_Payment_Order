using System.Text;
using Confluent.Kafka;
using PayFlow.Saga.Infrastructure.Messaging.Kafka;

namespace PayFlow.Saga.UnitTests.Messaging.Kafka;

public sealed class OrderCreatedKafkaMessageParserTests
{
    private static readonly Guid MessageId =
        Guid.Parse("7a655e6f-e385-4a55-9ba1-c5eb59212faf");

    private static readonly Guid OrderId =
        Guid.Parse("15ae1777-7fd4-4ef0-b9ac-3f3b9c89bfc1");

    private static readonly Guid CustomerId =
        Guid.Parse("07c943d2-4960-4645-aa7d-aa754ae4cbde");

    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAtUtc =
        OccurredAtUtc.AddSeconds(1);

    [Fact]
    public void ParseMapsValidOrderCreatedRecord()
    {
        var record = CreateRecord();

        var actual =
            OrderCreatedKafkaMessageParser.Parse(
                record,
                ReceivedAtUtc);

        Assert.Equal(
            MessageId,
            actual.Message.Envelope.MessageId);
        Assert.Equal(
            "OrderCreated.v1",
            actual.Message.Envelope.MessageType);
        Assert.Equal(
            1,
            actual.Message.Envelope.SchemaVersion);
        Assert.Equal(
            OrderId,
            actual.Message.Envelope.AggregateId);
        Assert.Equal(
            OrderId,
            actual.Message.Payload.OrderId);
        Assert.Equal(
            CustomerId,
            actual.Message.Payload.CustomerId);
        Assert.Equal(
            "orders.events",
            actual.SourceTopic);
        Assert.Equal(1, actual.SourcePartition);
        Assert.Equal(42, actual.SourceOffset);
        Assert.Equal(
            ReceivedAtUtc,
            actual.ReceivedAtUtc);
    }

    [Fact]
    public void ParseRejectsMissingRequiredHeader()
    {
        var record = CreateRecord();
        record.Message.Headers.Remove("message-id");

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "message-id",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsUnsupportedSchemaVersion()
    {
        var record = CreateRecord(
            schemaVersion: "2");

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "schema version",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRejectsKafkaKeyThatDoesNotMatchAggregate()
    {
        var record = CreateRecord();
        record.Message.Key = Guid.NewGuid().ToString("D");

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "Kafka key",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsPayloadOrderIdThatDoesNotMatchAggregate()
    {
        var record = CreateRecord(
            payloadOrderId: Guid.NewGuid());

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "Payload OrderId",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsMalformedJson()
    {
        var record = CreateRecord();
        record.Message.Value = "{not-json}";

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "JSON",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsDuplicateRequiredHeader()
    {
        var record = CreateRecord();
        record.Message.Headers.Add(
            "message-id",
            Encoding.UTF8.GetBytes(
                Guid.NewGuid().ToString("D")));

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    OrderCreatedKafkaMessageParser.Parse(
                        record,
                        ReceivedAtUtc));

        Assert.Contains(
            "must not be duplicated",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ConsumeResult<string, string> CreateRecord(
        string schemaVersion = "1",
        Guid? payloadOrderId = null)
    {
        var headers = new Headers();

        AddHeader(
            headers,
            "message-id",
            MessageId.ToString("D"));
        AddHeader(
            headers,
            "message-type",
            "OrderCreated.v1");
        AddHeader(
            headers,
            "schema-version",
            schemaVersion);
        AddHeader(
            headers,
            "aggregate-id",
            OrderId.ToString("D"));
        AddHeader(
            headers,
            "correlation-id",
            OrderId.ToString("D"));
        AddHeader(
            headers,
            "occurred-at-utc",
            OccurredAtUtc.ToString("O"));
        AddHeader(
            headers,
            "producer",
            "Order");

        var orderId =
            payloadOrderId ?? OrderId;

        var payload =
            $$"""
              {
                "orderId":"{{orderId:D}}",
                "customerId":"{{CustomerId:D}}",
                "currency":"USD",
                "totalAmount":35.0,
                "items":[
                  {
                    "skuId":"SKU-001",
                    "quantity":2,
                    "unitPrice":17.5
                  }
                ]
              }
              """;

        return new ConsumeResult<string, string>
        {
            Topic = "orders.events",
            Partition = new Partition(1),
            Offset = new Offset(42),
            Message = new Message<string, string>
            {
                Key = OrderId.ToString("D"),
                Value = payload,
                Headers = headers
            }
        };
    }

    private static void AddHeader(
        Headers headers,
        string name,
        string value)
    {
        headers.Add(
            name,
            Encoding.UTF8.GetBytes(value));
    }
}
