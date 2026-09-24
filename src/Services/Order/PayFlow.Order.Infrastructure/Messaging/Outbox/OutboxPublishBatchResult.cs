namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed record OutboxPublishBatchResult(
    int ClaimedCount,
    int PublishedCount,
    int FailedCount);
