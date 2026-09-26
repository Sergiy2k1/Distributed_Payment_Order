namespace PayFlow.Inventory.Worker.HostedServices;

public sealed class OutboxPublisherWorkerOptions
{
    public OutboxPublisherWorkerOptions(
        bool enabled,
        TimeSpan pollInterval)
    {
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval));
        }

        Enabled = enabled;
        PollInterval = pollInterval;
    }

    public bool Enabled { get; }
    public TimeSpan PollInterval { get; }
}
