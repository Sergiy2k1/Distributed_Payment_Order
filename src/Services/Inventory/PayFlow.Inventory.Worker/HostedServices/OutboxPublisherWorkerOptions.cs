namespace PayFlow.Inventory.Worker.HostedServices;

public sealed class OutboxPublisherWorkerOptions
{
    public OutboxPublisherWorkerOptions(
        bool enabled,
        TimeSpan pollInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            pollInterval,
            TimeSpan.Zero);

        Enabled = enabled;
        PollInterval = pollInterval;
    }

    public bool Enabled { get; }
    public TimeSpan PollInterval { get; }
}
