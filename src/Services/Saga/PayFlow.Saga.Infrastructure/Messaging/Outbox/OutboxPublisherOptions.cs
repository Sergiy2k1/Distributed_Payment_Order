namespace PayFlow.Saga.Infrastructure.Messaging.Outbox;

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
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                leaseDuration,
                "Lease duration must be greater than zero.");
        }

        if (baseRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseRetryDelay),
                baseRetryDelay,
                "Base retry delay must be greater than zero.");
        }

        if (maxRetryDelay < baseRetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetryDelay),
                maxRetryDelay,
                "Maximum retry delay must be greater than or equal to the base retry delay.");
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
