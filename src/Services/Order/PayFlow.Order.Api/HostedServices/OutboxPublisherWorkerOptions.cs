namespace PayFlow.Order.Api.HostedServices;

public sealed class OutboxPublisherWorkerOptions
{
    public OutboxPublisherWorkerOptions(
        bool enabled,
        TimeSpan pollInterval)
    {
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                pollInterval,
                "Poll interval must be greater than zero.");
        }

        Enabled = enabled;
        PollInterval = pollInterval;
    }

    public bool Enabled { get; }

    public TimeSpan PollInterval { get; }
}
