using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Entities;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed class OrderProcessingStartedInboxProcessor
{
    public const string ConsumerName =
        "payflow.saga.checkout.v1";

    private readonly SagaDbContext _dbContext;
    private readonly IInboxMessageRepository _inboxRepository;
    private readonly IOrderProcessingStartedMessageHandler _handler;

    public OrderProcessingStartedInboxProcessor(
        SagaDbContext dbContext,
        IInboxMessageRepository inboxRepository,
        IOrderProcessingStartedMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedOrderProcessingStartedMessage consumedMessage,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumedMessage);
        EnsureUtc(
            consumedMessage.ReceivedAtUtc,
            nameof(consumedMessage));
        EnsureUtc(
            processedAtUtc,
            nameof(processedAtUtc));

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
            await transaction
                .CommitAsync(cancellationToken)
                .ConfigureAwait(false);

            return false;
        }

        try
        {
            await _handler
                .HandleAsync(
                    consumedMessage.Message,
                    cancellationToken)
                .ConfigureAwait(false);

            await _inboxRepository
                .MarkProcessedAsync(
                    ConsumerName,
                    envelope.MessageId,
                    processedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);

            await transaction
                .CommitAsync(cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            await transaction
                .RollbackAsync(cancellationToken)
                .ConfigureAwait(false);

            throw;
        }
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
