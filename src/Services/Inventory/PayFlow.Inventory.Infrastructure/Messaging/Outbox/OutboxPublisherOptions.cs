namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherOptions
{
    public OutboxPublisherOptions(
        int batchSize,
        TimeSpan leaseDuration,
        TimeSpan baseRetryDelay,
        TimeSpan maxRetryDelay)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize));
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration));
        }

        if (baseRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseRetryDelay));
        }

        if (maxRetryDelay < baseRetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetryDelay));
        }

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
