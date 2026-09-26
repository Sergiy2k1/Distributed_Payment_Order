using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Repositories;

public sealed class InboxMessageRepository
    : IInboxMessageRepository
{
    private readonly SagaDbContext _dbContext;

    public InboxMessageRepository(
        SagaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryInsertAsync(
        InboxMessageEntity message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var affectedRows = await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
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
                     {message.ProcessedAtUtc}
                 )
                 ON CONFLICT (consumer_name, message_id)
                 DO NOTHING
                 """,
                cancellationToken)
            .ConfigureAwait(false);

        return affectedRows == 1;
    }
}
