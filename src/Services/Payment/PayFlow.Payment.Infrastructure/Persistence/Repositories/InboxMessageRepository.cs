using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence.Repositories;

public sealed class InboxMessageRepository
{
    private readonly PaymentDbContext _dbContext;

    public InboxMessageRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryInsertAsync(
        InboxMessageEntity message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var affectedRows =
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO inbox_messages
                 (
                     consumer_name,
                     message_id,
                     message_type,
                     schema_version,
                     aggregate_id,
                     correlation_id,
                     causation_id,
                     occurred_at_utc,
                     source_topic,
                     source_partition,
                     source_offset,
                     received_at_utc,
                     processed_at_utc
                 )
                 VALUES
                 (
                     {message.ConsumerName},
                     {message.MessageId},
                     {message.MessageType},
                     {message.SchemaVersion},
                     {message.AggregateId},
                     {message.CorrelationId},
                     {message.CausationId},
                     {message.OccurredAtUtc},
                     {message.SourceTopic},
                     {message.SourcePartition},
                     {message.SourceOffset},
                     {message.ReceivedAtUtc},
                     NULL
                 )
                 ON CONFLICT (consumer_name, message_id)
                 DO NOTHING
                 """,
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task MarkProcessedAsync(
        string consumerName,
        Guid messageId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows =
            await _dbContext.InboxMessages
                .Where(message =>
                    message.ConsumerName == consumerName
                    && message.MessageId == messageId
                    && message.ProcessedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        message => message.ProcessedAtUtc,
                        processedAtUtc),
                    cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                "Inbox message is missing or already processed.");
        }
    }
}
