using PayFlow.Order.Application.Orders.ConfirmOrder;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Entities;
using PayFlow.Order.Infrastructure.Persistence.Repositories;

namespace PayFlow.Order.Infrastructure.Messaging;

public sealed class ConfirmOrderInboxProcessor
{
    public const string ConsumerName =
        "payflow.order.commands.v1";

    private readonly OrderDbContext _dbContext;
    private readonly IInboxMessageRepository _inboxRepository;
    private readonly IConfirmOrderMessageHandler _handler;

    public ConfirmOrderInboxProcessor(
        OrderDbContext dbContext,
        IInboxMessageRepository inboxRepository,
        IConfirmOrderMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedConfirmOrderMessage consumedMessage,
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
            await transaction.CommitAsync(cancellationToken)
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

            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }
}
