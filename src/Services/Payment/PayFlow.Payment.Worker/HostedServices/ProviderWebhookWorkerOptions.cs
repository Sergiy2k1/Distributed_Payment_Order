namespace PayFlow.Payment.Worker.HostedServices;

public sealed class ProviderWebhookWorkerOptions
{
    public ProviderWebhookWorkerOptions(
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
