using System.Text.Json;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Messaging.Outbox;

public sealed class SagaOutboxWriter
    : ISagaOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SagaDbContext _dbContext;

    public SagaOutboxWriter(
        SagaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        OutgoingIntegrationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Envelope);
        ArgumentNullException.ThrowIfNull(message.Payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Destination);

        if (message.Envelope.MessageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Outgoing message ID cannot be empty.",
                nameof(message));
        }

        var payload =
            JsonSerializer.Serialize(
                message.Payload,
                message.Payload.GetType(),
                SerializerOptions);

        var entity =
            new OutboxMessageEntity
            {
                OutboxMessageId = Guid.NewGuid(),
                MessageId = message.Envelope.MessageId,
                MessageType = message.Envelope.MessageType,
                SchemaVersion = message.Envelope.SchemaVersion,
                AggregateId = message.Envelope.AggregateId,
                CorrelationId = message.Envelope.CorrelationId,
                CausationId = message.Envelope.CausationId,
                OccurredAtUtc = message.Envelope.OccurredAtUtc,
                Destination = message.Destination,
                Producer = message.Envelope.Producer,
                TraceParent = message.Envelope.TraceParent,
                Payload = payload,
                CreatedAtUtc = message.Envelope.OccurredAtUtc,
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
