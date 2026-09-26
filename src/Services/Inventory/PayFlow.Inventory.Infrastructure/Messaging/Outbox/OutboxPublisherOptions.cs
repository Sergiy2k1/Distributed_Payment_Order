namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherOptions
{
    public OutboxPublisherOptions(
        int batchSize,
        TimeSpan leaseDuration,
        TimeSpan baseRetryDelay,
        TimeSpan maxRetryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            batchSize);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            baseRetryDelay,
            TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxRetryDelay,
            baseRetryDelay);

        BatchSize = batchSize;
        LeaseDuration = leaseDuration;
        BaseRetryDelay = baseRetryDelay;
        MaxRetryDelay = maxRetryDelay;
    }

    public int BatchSize { get; }
    public TimeSpan LeaseDuration { get; }
    public TimeSpan BaseRetryDelay { get; }
    public TimeSpan MaxRetryDelay { get; }
}
