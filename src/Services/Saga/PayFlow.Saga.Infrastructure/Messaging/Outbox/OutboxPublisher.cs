using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Messaging.Outbox;

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
        var claimed = await _repository
            .ClaimPendingAsync(
                _timeProvider.GetUtcNow(),
                _options.LeaseDuration,
                _options.BatchSize,
                claimToken,
                cancellationToken)
            .ConfigureAwait(false);

        var publishedCount = 0;
        var failedCount = 0;

        foreach (var message in claimed)
        {
            try
            {
                await _transport
                    .PublishAsync(
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
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
                        cancellationToken)
                    .ConfigureAwait(false);

                failedCount++;
                continue;
            }

            await _repository
                .MarkPublishedAsync(
                    message.OutboxMessageId,
                    claimToken,
                    _timeProvider.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);

            publishedCount++;
        }

        return new OutboxPublishBatchResult(
            claimed.Count,
            publishedCount,
            failedCount);
    }

    private async Task MarkFailedAsync(
        OutboxMessageEntity message,
        Guid claimToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAtUtc = _timeProvider.GetUtcNow();
        var retryDelay = _retryPolicy.GetDelay(
            message.AttemptCount);
        var errorCode = GetErrorCode(exception);

        await _repository
            .MarkFailedAsync(
                message.OutboxMessageId,
                claimToken,
                failedAtUtc,
                failedAtUtc.Add(retryDelay),
                errorCode,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetErrorCode(Exception exception)
    {
        var errorCode = exception.GetType().Name;

        return errorCode.Length <= 128
            ? errorCode
            : errorCode[..128];
    }
}
