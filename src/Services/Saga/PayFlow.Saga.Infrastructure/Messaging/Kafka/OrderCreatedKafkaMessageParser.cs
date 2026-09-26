using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;

namespace PayFlow.Saga.Infrastructure.Messaging.Kafka;

public static class OrderCreatedKafkaMessageParser
{
    public const string Topic = "orders.events";
    public const string MessageType = "OrderCreated.v1";
    public const int SchemaVersion = 1;
    public const string Producer = "Order";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static ConsumedOrderCreatedMessage Parse(
        ConsumeResult<string, string> consumeResult,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(consumeResult);

        if (receivedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                "Received timestamp must use UTC offset.");
        }

        if (!string.Equals(
                consumeResult.Topic,
                Topic,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Expected Kafka topic '{Topic}'.");
        }

        if (consumeResult.Partition.Value < 0)
        {
            throw new InvalidDataException(
                "Kafka partition must be non-negative.");
        }

        if (consumeResult.Offset.Value < 0)
        {
            throw new InvalidDataException(
                "Kafka offset must be non-negative.");
        }

        var kafkaMessage = consumeResult.Message
            ?? throw new InvalidDataException(
                "Kafka message is missing.");

        var messageId = ParseGuidHeader(
            kafkaMessage.Headers,
            "message-id");
        var messageType = GetRequiredHeader(
            kafkaMessage.Headers,
            "message-type");
        var schemaVersion = ParseIntHeader(
            kafkaMessage.Headers,
            "schema-version");
        var aggregateId = ParseGuidHeader(
            kafkaMessage.Headers,
            "aggregate-id");
        var correlationId = ParseGuidHeader(
            kafkaMessage.Headers,
            "correlation-id");
        var causationId = ParseOptionalGuidHeader(
            kafkaMessage.Headers,
            "causation-id");
        var occurredAtUtc = ParseUtcHeader(
            kafkaMessage.Headers,
            "occurred-at-utc");
        var producer = GetRequiredHeader(
            kafkaMessage.Headers,
            "producer");
        var traceParent = GetOptionalHeader(
            kafkaMessage.Headers,
            "traceparent");

        if (!string.Equals(
                messageType,
                MessageType,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported message type '{messageType}'.");
        }

        if (schemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported schema version '{schemaVersion}'.");
        }

        if (!string.Equals(
                producer,
                Producer,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unexpected producer '{producer}'.");
        }

        var expectedKey = aggregateId.ToString("D");

        if (!string.Equals(
                kafkaMessage.Key,
                expectedKey,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Kafka key must be the canonical OrderId.");
        }

        if (string.IsNullOrWhiteSpace(kafkaMessage.Value))
        {
            throw new InvalidDataException(
                "Kafka payload is missing.");
        }

        OrderCreatedV1 payload;

        try
        {
            payload = JsonSerializer.Deserialize<OrderCreatedV1>(
                kafkaMessage.Value,
                SerializerOptions)
                ?? throw new InvalidDataException(
                    "Kafka payload deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Kafka payload is not valid OrderCreated.v1 JSON.",
                exception);
        }

        ValidatePayload(
            payload,
            aggregateId);

        return new ConsumedOrderCreatedMessage(
            new OrderCreatedMessage(
                new IntegrationMessageEnvelope(
                    messageId,
                    messageType,
                    schemaVersion,
                    aggregateId,
                    correlationId,
                    causationId,
                    occurredAtUtc,
                    producer,
                    traceParent),
                payload),
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            receivedAtUtc);
    }

    private static void ValidatePayload(
        OrderCreatedV1 payload,
        Guid aggregateId)
    {
        if (payload.OrderId == Guid.Empty)
        {
            throw new InvalidDataException(
                "Payload OrderId cannot be empty.");
        }

        if (payload.OrderId != aggregateId)
        {
            throw new InvalidDataException(
                "Payload OrderId must match aggregate-id.");
        }

        if (payload.CustomerId == Guid.Empty)
        {
            throw new InvalidDataException(
                "Payload CustomerId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(payload.Currency)
            || payload.Currency.Length != 3)
        {
            throw new InvalidDataException(
                "Payload currency must contain three characters.");
        }

        if (payload.TotalAmount < 0)
        {
            throw new InvalidDataException(
                "Payload total amount cannot be negative.");
        }

        if (payload.Items is null
            || payload.Items.Count == 0)
        {
            throw new InvalidDataException(
                "Payload must contain at least one item.");
        }

        foreach (var item in payload.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SkuId))
            {
                throw new InvalidDataException(
                    "Payload item SKU cannot be empty.");
            }

            if (item.Quantity <= 0)
            {
                throw new InvalidDataException(
                    "Payload item quantity must be positive.");
            }

            if (item.UnitPrice <= 0)
            {
                throw new InvalidDataException(
                    "Payload item unit price must be positive.");
            }
        }
    }

    private static string GetRequiredHeader(
        Headers headers,
        string name)
    {
        var value = GetOptionalHeader(
            headers,
            name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Required Kafka header '{name}' is missing.");
        }

        return value;
    }

    private static string? GetOptionalHeader(
        Headers headers,
        string name)
    {
        var matches = headers
            .Where(header =>
                string.Equals(
                    header.Key,
                    name,
                    StringComparison.Ordinal))
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidDataException(
                $"Kafka header '{name}' must not be duplicated.");
        }

        if (matches.Length == 0)
        {
            return null;
        }

        var bytes = matches[0].GetValueBytes();

        if (bytes is null)
        {
            return null;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static Guid ParseGuidHeader(
        Headers headers,
        string name)
    {
        var value = GetRequiredHeader(
            headers,
            name);

        if (!Guid.TryParseExact(
                value,
                "D",
                out var parsed)
            || parsed == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Kafka header '{name}' must be a non-empty canonical GUID.");
        }

        return parsed;
    }

    private static Guid? ParseOptionalGuidHeader(
        Headers headers,
        string name)
    {
        var value = GetOptionalHeader(
            headers,
            name);

        if (value is null)
        {
            return null;
        }

        if (!Guid.TryParseExact(
                value,
                "D",
                out var parsed)
            || parsed == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Kafka header '{name}' must be a non-empty canonical GUID.");
        }

        return parsed;
    }

    private static int ParseIntHeader(
        Headers headers,
        string name)
    {
        var value = GetRequiredHeader(
            headers,
            name);

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed)
            || parsed <= 0)
        {
            throw new InvalidDataException(
                $"Kafka header '{name}' must be a positive integer.");
        }

        return parsed;
    }

    private static DateTimeOffset ParseUtcHeader(
        Headers headers,
        string name)
    {
        var value = GetRequiredHeader(
            headers,
            name);

        if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"Kafka header '{name}' must be a UTC round-trip timestamp.");
        }

        return parsed;
    }
}
