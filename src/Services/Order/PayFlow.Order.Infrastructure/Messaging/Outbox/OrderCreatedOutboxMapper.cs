using System.Text.Json;
using PayFlow.Order.Application.IntegrationEvents.Orders;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public static class OrderCreatedOutboxMapper
{
    public const string MessageType = "OrderCreated.v1";
    public const int SchemaVersion = 1;
    public const string Destination = "orders.events";
    public const string Producer = "Order";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static OutboxMessageEntity Map(
        OrderCreatedDomainEvent domainEvent,
        Guid outboxMessageId,
        Guid messageId,
        string? traceParent = null)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (outboxMessageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Outbox message ID cannot be empty.",
                nameof(outboxMessageId));
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Message ID cannot be empty.",
                nameof(messageId));
        }

        var contract = new OrderCreatedV1(
            domainEvent.OrderId.Value,
            domainEvent.CustomerId.Value,
            domainEvent.Total.Currency,
            domainEvent.Total.Amount,
            domainEvent.Items
                .Select(static item =>
                    new OrderCreatedItemV1(
                        item.Sku.Value,
                        item.Quantity,
                        item.UnitPrice.Amount))
                .ToArray());

        var payload = JsonSerializer.Serialize(
            contract,
            SerializerOptions);

        return new OutboxMessageEntity
        {
            OutboxMessageId = outboxMessageId,
            MessageId = messageId,
            MessageType = MessageType,
            SchemaVersion = SchemaVersion,
            AggregateId = domainEvent.OrderId.Value,
            CorrelationId = domainEvent.OrderId.Value,
            CausationId = null,
            OccurredAtUtc = domainEvent.OccurredAtUtc,
            Destination = Destination,
            Producer = Producer,
            TraceParent = traceParent,
            Payload = payload,
            CreatedAtUtc = domainEvent.OccurredAtUtc,
            PublishedAtUtc = null,
            AttemptCount = 0,
            NextAttemptAtUtc = null,
            LastErrorCode = null
        };
    }
}
