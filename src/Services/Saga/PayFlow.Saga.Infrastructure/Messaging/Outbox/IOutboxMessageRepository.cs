using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Messaging.Outbox;

public interface IOutboxMessageRepository
{
    Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        Guid claimToken,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(
        Guid outboxMessageId,
        Guid claimToken,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid outboxMessageId,
        Guid claimToken,
        DateTimeOffset failedAtUtc,
        DateTimeOffset nextAttemptAtUtc,
        string errorCode,
        CancellationToken cancellationToken = default);
}
