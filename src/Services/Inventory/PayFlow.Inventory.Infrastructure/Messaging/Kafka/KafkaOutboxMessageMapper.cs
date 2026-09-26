using System.Globalization;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public static class KafkaOutboxMessageMapper
{
    public static KafkaPublishRequest Map(
        OutboxMessageEntity message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var headers = new List<KafkaHeader>
        {
            new("message-id", message.MessageId.ToString("D")),
            new("message-type", message.MessageType),
            new(
                "schema-version",
                message.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture)),
            new("aggregate-id", message.AggregateId.ToString("D")),
            new("correlation-id", message.CorrelationId.ToString("D")),
            new(
                "occurred-at-utc",
                message.OccurredAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture)),
            new("producer", message.Producer)
        };

        if (message.CausationId is { } causationId)
        {
            headers.Add(
                new KafkaHeader(
                    "causation-id",
                    causationId.ToString("D")));
        }

        if (!string.IsNullOrWhiteSpace(
                message.TraceParent))
        {
            headers.Add(
                new KafkaHeader(
                    "traceparent",
                    message.TraceParent));
        }

        return new KafkaPublishRequest(
            message.Destination,
            message.AggregateId.ToString("D"),
            message.Payload,
            headers);
    }
}
