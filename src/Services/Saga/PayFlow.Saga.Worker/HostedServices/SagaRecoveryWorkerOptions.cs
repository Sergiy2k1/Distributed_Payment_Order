namespace PayFlow.Saga.Worker.HostedServices;

public sealed class SagaRecoveryWorkerOptions
{
    public SagaRecoveryWorkerOptions(
        bool enabled,
        TimeSpan pollInterval,
        int batchSize)
    {
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                pollInterval,
                "Poll interval must be greater than zero.");
        }

        if (batchSize <= 0 || batchSize > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be between 1 and 1000.");
        }

        Enabled = enabled;
        PollInterval = pollInterval;
        BatchSize = batchSize;
    }

    public bool Enabled { get; }

    public TimeSpan PollInterval { get; }

    public int BatchSize { get; }
}
