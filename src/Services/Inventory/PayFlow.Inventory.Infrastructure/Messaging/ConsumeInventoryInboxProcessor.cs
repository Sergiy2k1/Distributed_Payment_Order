using PayFlow.Inventory.Application.Reservations;
using PayFlow.Inventory.Infrastructure.Persistence;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;

namespace PayFlow.Inventory.Infrastructure.Messaging;

public sealed class ConsumeInventoryInboxProcessor
{
    public const string ConsumerName =
        "payflow.inventory.commands.v1";

    private readonly InventoryDbContext _dbContext;
    private readonly IInboxMessageRepository _inboxRepository;
    private readonly IConsumeInventoryMessageHandler _handler;

    public ConsumeInventoryInboxProcessor(
        InventoryDbContext dbContext,
        IInboxMessageRepository inboxRepository,
        IConsumeInventoryMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedConsumeInventoryMessage consumedMessage,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumedMessage);

        var envelope = consumedMessage.Message.Envelope;

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

        var inserted =
            await _inboxRepository.TryInsertAsync(
                new InboxMessageEntity
                {
                    ConsumerName = ConsumerName,
                    MessageId = envelope.MessageId,
                    MessageType = envelope.MessageType,
                    SchemaVersion = envelope.SchemaVersion,
                    AggregateId = envelope.AggregateId,
                    CorrelationId = envelope.CorrelationId,
                    CausationId = envelope.CausationId,
                    OccurredAtUtc = envelope.OccurredAtUtc,
                    SourceTopic = consumedMessage.SourceTopic,
                    SourcePartition = consumedMessage.SourcePartition,
                    SourceOffset = consumedMessage.SourceOffset,
                    ReceivedAtUtc = consumedMessage.ReceivedAtUtc,
                    ProcessedAtUtc = null
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!inserted)
        {
            await transaction.CommitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            return false;
        }

        try
        {
            await _handler.HandleAsync(
                consumedMessage.Message,
                cancellationToken)
                .ConfigureAwait(false);

            await _inboxRepository.MarkProcessedAsync(
                ConsumerName,
                envelope.MessageId,
                processedAtUtc,
                cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken)
                .ConfigureAwait(false);

            throw;
        }
    }
}
