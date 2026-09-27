using PayFlow.Payment.Application.Refund;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Entities;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;

namespace PayFlow.Payment.Infrastructure.Messaging;

public sealed class RefundPaymentInboxProcessor
{
    public const string ConsumerName =
        "payflow.payment.commands.v1";

    private readonly PaymentDbContext _dbContext;
    private readonly InboxMessageRepository _inboxRepository;
    private readonly IRefundPaymentMessageHandler _handler;

    public RefundPaymentInboxProcessor(
        PaymentDbContext dbContext,
        InboxMessageRepository inboxRepository,
        IRefundPaymentMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedRefundPaymentMessage consumedMessage,
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
                    ReceivedAtUtc = consumedMessage.ReceivedAtUtc
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
