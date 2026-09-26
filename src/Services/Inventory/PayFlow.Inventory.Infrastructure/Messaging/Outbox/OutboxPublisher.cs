using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisher
{
    private readonly IOutboxMessageRepository _repository;
    private readonly IOutboxTransport _transport;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxPublisherOptions _options;
    private readonly OutboxRetryPolicy _retryPolicy;

    public OutboxPublisher(
        IOutboxMessageRepository repository,
        IOutboxTransport transport,
        TimeProvider timeProvider,
        OutboxPublisherOptions options)
    {
        _repository = repository;
        _transport = transport;
        _timeProvider = timeProvider;
        _options = options;
        _retryPolicy = new OutboxRetryPolicy(
            options.BaseRetryDelay,
            options.MaxRetryDelay);
    }

    public async Task<OutboxPublishBatchResult> PublishBatchAsync(
        CancellationToken cancellationToken = default)
    {
        var claimToken = Guid.NewGuid();

        var messages = await _repository
            .ClaimPendingAsync(
                _timeProvider.GetUtcNow(),
                _options.LeaseDuration,
                _options.BatchSize,
                claimToken,
                cancellationToken)
            .ConfigureAwait(false);

        var published = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            try
            {
                await _transport.PublishAsync(
                    message,
                    cancellationToken);

                await _repository.MarkPublishedAsync(
                    message.OutboxMessageId,
                    claimToken,
                    _timeProvider.GetUtcNow(),
                    cancellationToken);

                published++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await MarkFailedAsync(
                    message,
                    claimToken,
                    exception,
                    cancellationToken);

                failed++;
            }
        }

        return new OutboxPublishBatchResult(
            messages.Count,
            published,
            failed);
    }

    private Task MarkFailedAsync(
        OutboxMessageEntity message,
        Guid claimToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAtUtc =
            _timeProvider.GetUtcNow();

        return _repository.MarkFailedAsync(
            message.OutboxMessageId,
            claimToken,
            failedAtUtc,
            failedAtUtc.Add(
                _retryPolicy.GetDelay(
                    message.AttemptCount)),
            exception.GetType().Name,
            cancellationToken);
    }
}
