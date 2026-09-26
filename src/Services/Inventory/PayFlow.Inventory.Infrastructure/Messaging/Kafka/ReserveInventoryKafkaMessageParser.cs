using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using PayFlow.Inventory.Application.Messaging;
using PayFlow.Inventory.Application.Reservations;

namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public static class ReserveInventoryKafkaMessageParser
{
    public const string Topic = "inventory.commands";
    public const string MessageType = "ReserveInventory.v1";
    public const int SchemaVersion = 1;
    public const string Producer = "Saga";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static ConsumedReserveInventoryMessage Parse(
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

        if (consumeResult.Partition.Value < 0
            || consumeResult.Offset.Value < 0)
        {
            throw new InvalidDataException(
                "Kafka source position must be non-negative.");
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

        if (!string.Equals(
                kafkaMessage.Key,
                aggregateId.ToString("D"),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Kafka key must be the canonical OrderId.");
        }

        if (string.IsNullOrWhiteSpace(
                kafkaMessage.Value))
        {
            throw new InvalidDataException(
                "Kafka payload is missing.");
        }

        ReserveInventoryV1 payload;

        try
        {
            payload =
                JsonSerializer.Deserialize<ReserveInventoryV1>(
                    kafkaMessage.Value,
                    SerializerOptions)
                ?? throw new InvalidDataException(
                    "Kafka payload deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Kafka payload is not valid ReserveInventory.v1 JSON.",
                exception);
        }

        if (payload.OrderId == Guid.Empty
            || payload.OrderId != aggregateId)
        {
            throw new InvalidDataException(
                "Payload OrderId must match aggregate-id.");
        }

        return new ConsumedReserveInventoryMessage(
            new ReserveInventoryMessage(
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

        return bytes is null
            ? null
            : Encoding.UTF8.GetString(bytes);
    }

    private static Guid ParseGuidHeader(
        Headers headers,
        string name)
    {
        var value = GetRequiredHeader(headers, name);

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
        var value = GetOptionalHeader(headers, name);

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
        var value = GetRequiredHeader(headers, name);

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
        var value = GetRequiredHeader(headers, name);

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
