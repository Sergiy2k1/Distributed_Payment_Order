using System.Text.Json;
using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Infrastructure.Persistence;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed class InventoryOutboxWriter
    : IInventoryOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly InventoryDbContext _dbContext;

    public InventoryOutboxWriter(
        InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Guid orderId,
        Guid correlationId,
        Guid causationId,
        DateTimeOffset occurredAtUtc,
        string messageType,
        object payload,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            messageType);

        var entity =
            new OutboxMessageEntity
            {
                OutboxMessageId = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                MessageType = messageType,
                SchemaVersion = 1,
                AggregateId = orderId,
                CorrelationId = correlationId,
                CausationId = causationId,
                OccurredAtUtc = occurredAtUtc,
                Destination = "inventory.events",
                Producer = "Inventory",
                TraceParent = traceParent,
                Payload = JsonSerializer.Serialize(
                    payload,
                    payload.GetType(),
                    SerializerOptions),
                CreatedAtUtc = occurredAtUtc,
                PublishedAtUtc = null,
                AttemptCount = 0,
                NextAttemptAtUtc = null,
                LastErrorCode = null,
                ClaimToken = null,
                ClaimedUntilUtc = null
            };

        await _dbContext.OutboxMessages
            .AddAsync(
                entity,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
