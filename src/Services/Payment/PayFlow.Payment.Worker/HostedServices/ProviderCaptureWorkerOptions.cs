namespace PayFlow.Payment.Worker.HostedServices;

public sealed class ProviderCaptureWorkerOptions
{
    public ProviderCaptureWorkerOptions(
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
