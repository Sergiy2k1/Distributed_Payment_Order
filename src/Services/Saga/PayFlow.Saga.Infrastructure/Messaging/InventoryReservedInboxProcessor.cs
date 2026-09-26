using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Entities;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed class InventoryReservedInboxProcessor
{
    public const string ConsumerName =
        "payflow.saga.checkout.v1";

    private readonly SagaDbContext _dbContext;
    private readonly IInboxMessageRepository _inboxRepository;
    private readonly IInventoryReservedMessageHandler _handler;

    public InventoryReservedInboxProcessor(
        SagaDbContext dbContext,
        IInboxMessageRepository inboxRepository,
        IInventoryReservedMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedInventoryReservedMessage consumedMessage,
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
                CreateInboxMessage(
                    consumedMessage,
                    envelope),
                cancellationToken)
            .ConfigureAwait(false);

        if (!inserted)
        {
            await transaction.CommitAsync(
                cancellationToken);
            return false;
        }

        try
        {
            await _handler.HandleAsync(
                consumedMessage.Message,
                cancellationToken);

            await _inboxRepository.MarkProcessedAsync(
                ConsumerName,
                envelope.MessageId,
                processedAtUtc,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);
            throw;
        }
    }

    private static InboxMessageEntity CreateInboxMessage(
        ConsumedInventoryReservedMessage consumedMessage,
        PayFlow.Saga.Application.Messaging.IntegrationMessageEnvelope envelope)
    {
        return new InboxMessageEntity
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
            ReceivedAtUtc = consumedMessage.ReceivedAtUtc
        };
    }
}
