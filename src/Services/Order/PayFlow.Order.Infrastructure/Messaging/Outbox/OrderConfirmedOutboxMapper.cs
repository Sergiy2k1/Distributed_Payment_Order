using System.Text.Json;
using PayFlow.Order.Application.IntegrationEvents.Orders;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public static class OrderConfirmedOutboxMapper
{
    public const string MessageType = "OrderConfirmed.v1";
    public const int SchemaVersion = 1;
    public const string Destination = "orders.events";
    public const string Producer = "Order";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static OutboxMessageEntity Map(
        OrderStatusChangedDomainEvent domainEvent,
        Guid outboxMessageId,
        Guid messageId,
        Guid correlationId,
        Guid causationId,
        string? traceParent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.PreviousStatus != OrderStatus.Processing
            || domainEvent.CurrentStatus != OrderStatus.Confirmed)
        {
            throw new ArgumentException(
                "The domain event is not a Processing-to-Confirmed transition.",
                nameof(domainEvent));
        }

        var payload = JsonSerializer.Serialize(
            new OrderConfirmedV1(
                domainEvent.OrderId.Value,
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
