using System.Text.Json;
using PayFlow.Order.Application.IntegrationEvents.Orders;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public static class OrderCancelledOutboxMapper
{
    public const string MessageType = "OrderCancelled.v1";
    public const int SchemaVersion = 1;
    public const string Destination = "orders.events";
    public const string Producer = "Order";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static OutboxMessageEntity Map(
        OrderStatusChangedDomainEvent domainEvent,
        string reasonCode,
        Guid outboxMessageId,
        Guid messageId,
        Guid correlationId,
        Guid causationId,
        string? traceParent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        if (domainEvent.PreviousStatus != OrderStatus.Cancelling
            || domainEvent.CurrentStatus != OrderStatus.Cancelled)
        {
            throw new ArgumentException(
                "The domain event is not a Cancelling-to-Cancelled transition.",
                nameof(domainEvent));
        }

        var payload =
            JsonSerializer.Serialize(
                new OrderCancelledV1(
                    domainEvent.OrderId.Value,
                    reasonCode,
                    domainEvent.OccurredAtUtc),
                SerializerOptions);

        return new OutboxMessageEntity
        {
            OutboxMessageId = outboxMessageId,
            MessageId = messageId,
            MessageType = MessageType,
            SchemaVersion = SchemaVersion,
            AggregateId = domainEvent.OrderId.Value,
            CorrelationId = correlationId,
            CausationId = causationId,
            OccurredAtUtc = domainEvent.OccurredAtUtc,
            Destination = Destination,
            Producer = Producer,
            TraceParent = traceParent,
            Payload = payload,
            CreatedAtUtc = domainEvent.OccurredAtUtc,
            PublishedAtUtc = null,
            AttemptCount = 0,
            NextAttemptAtUtc = null,
            LastErrorCode = null,
            ClaimToken = null,
            ClaimedUntilUtc = null
        };
    }
}
