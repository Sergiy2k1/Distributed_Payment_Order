namespace PayFlow.Payment.Application.Provider;

public sealed class ProviderCaptureExecutorOptions
{
    public ProviderCaptureExecutorOptions(
        TimeSpan staleProcessingAfter,
        TimeSpan ambiguousRetryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            staleProcessingAfter,
            TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            ambiguousRetryDelay,
            TimeSpan.Zero);

        StaleProcessingAfter = staleProcessingAfter;
        AmbiguousRetryDelay = ambiguousRetryDelay;
    }

    public TimeSpan StaleProcessingAfter { get; }
    public TimeSpan AmbiguousRetryDelay { get; }
}
