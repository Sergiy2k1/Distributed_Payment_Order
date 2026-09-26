using System.Text.Json;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Messaging.Outbox;

public sealed class PaymentOutboxWriter
    : IPaymentOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly PaymentDbContext _dbContext;

    public PaymentOutboxWriter(
        PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Guid orderId,
        Guid correlationId,
        Guid? causationId,
        DateTimeOffset occurredAtUtc,
        string messageType,
        object payload,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);

        await _dbContext.OutboxMessages.AddAsync(
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
                Destination = "payments.events",
                Producer = "Payment",
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
            },
            cancellationToken)
            .ConfigureAwait(false);
    }
}
