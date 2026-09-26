using System.Text.Json;
using PayFlow.Order.Application.IntegrationEvents.Orders;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public static class OrderProcessingStartedOutboxMapper
{
    public const string MessageType =
        "OrderProcessingStarted.v1";
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

        if (domainEvent.PreviousStatus != OrderStatus.Pending
            || domainEvent.CurrentStatus != OrderStatus.Processing)
        {
            throw new ArgumentException(
                "The domain event is not a Pending-to-Processing transition.",
                nameof(domainEvent));
        }

        EnsureIdentity(
            outboxMessageId,
            nameof(outboxMessageId));
        EnsureIdentity(
            messageId,
            nameof(messageId));
        EnsureIdentity(
            correlationId,
            nameof(correlationId));
        EnsureIdentity(
            causationId,
            nameof(causationId));

        var payload =
            JsonSerializer.Serialize(
                new OrderProcessingStartedV1(
                    domainEvent.OrderId.Value),
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

    private static void EnsureIdentity(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty.",
                parameterName);
        }
    }
}
