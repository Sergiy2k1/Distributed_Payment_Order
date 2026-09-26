using PayFlow.Payment.Application.Capture;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Entities;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;

namespace PayFlow.Payment.Infrastructure.Messaging;

public sealed class CapturePaymentInboxProcessor
{
    public const string ConsumerName =
        "payflow.payment.commands.v1";

    private readonly PaymentDbContext _dbContext;
    private readonly InboxMessageRepository _inboxRepository;
    private readonly ICapturePaymentMessageHandler _handler;

    public CapturePaymentInboxProcessor(
        PaymentDbContext dbContext,
        InboxMessageRepository inboxRepository,
        ICapturePaymentMessageHandler handler)
    {
        _dbContext = dbContext;
        _inboxRepository = inboxRepository;
        _handler = handler;
    }

    public async Task<bool> ProcessAsync(
        ConsumedCapturePaymentMessage consumedMessage,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumedMessage);

        var envelope = consumedMessage.Message.Envelope;

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

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
                cancellationToken);

        if (!inserted)
        {
            await transaction.CommitAsync(cancellationToken);
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

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
