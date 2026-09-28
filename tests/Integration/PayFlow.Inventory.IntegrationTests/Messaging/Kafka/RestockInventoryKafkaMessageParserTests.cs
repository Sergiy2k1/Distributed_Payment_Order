using System.Text;
using Confluent.Kafka;
using PayFlow.Inventory.Infrastructure.Messaging.Kafka;

namespace PayFlow.Inventory.IntegrationTests.Messaging.Kafka;

public sealed class RestockInventoryKafkaMessageParserTests
{
    private static readonly Guid MessageId =
        Guid.Parse(
            "d14f0d31-e9c2-4135-b6f2-b17d7d9f155e");

    private static readonly Guid OrderId =
        Guid.Parse(
            "aa950314-1194-4b41-b113-d59fb0925413");

    private static readonly Guid ReservationId =
        Guid.Parse(
            "499be8cc-ca8e-4ac3-bbba-f456b7af2654");

    private static readonly Guid RestockOperationId =
        Guid.Parse(
            "f3845211-0f67-462f-90e6-ddc3e6cf55de");

    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 29, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ParseMapsValidRestockInventoryRecord()
    {
        var actual =
            RestockInventoryKafkaMessageParser.Parse(
                CreateRecord(),
                OccurredAtUtc.AddSeconds(1));

        Assert.Equal(
            MessageId,
            actual.Message.Envelope.MessageId);
        Assert.Equal(
            OrderId,
            actual.Message.Payload.OrderId);
        Assert.Equal(
            ReservationId,
            actual.Message.Payload.ReservationId);
        Assert.Equal(
            RestockOperationId,
            actual.Message.Payload.RestockOperationId);
        Assert.Equal(
            "POST_CAPTURE_COMPENSATION",
            actual.Message.Payload.ReasonCode);
        Assert.Equal(
            "inventory.commands",
            actual.SourceTopic);
        Assert.Equal(2, actual.SourcePartition);
        Assert.Equal(51, actual.SourceOffset);
    }

    [Fact]
    public void ParseRejectsMissingRestockOperationId()
    {
        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    RestockInventoryKafkaMessageParser.Parse(
                        CreateRecord(
                            restockOperationId:
                                Guid.Empty),
                        OccurredAtUtc.AddSeconds(1)));

        Assert.Contains(
            "payload",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ConsumeResult<string, string> CreateRecord(
        Guid? restockOperationId = null)
    {
        var headers =
            new Headers();

        AddHeader(
            headers,
            "message-id",
            MessageId.ToString("D"));
        AddHeader(
            headers,
            "message-type",
            "RestockInventory.v1");
        AddHeader(
            headers,
            "schema-version",
            "1");
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
            "Saga");

        var operationId =
            restockOperationId
            ?? RestockOperationId;

        var payload =
            $$"""
              {
                "orderId":"{{OrderId:D}}",
                "reservationId":"{{ReservationId:D}}",
                "restockOperationId":"{{operationId:D}}",
                "reasonCode":"POST_CAPTURE_COMPENSATION"
              }
              """;

        return new ConsumeResult<string, string>
        {
            Topic = "inventory.commands",
            Partition = new Partition(2),
            Offset = new Offset(51),
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
