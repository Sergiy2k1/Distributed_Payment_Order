namespace PayFlow.MockPaymentProvider;

public sealed class MockPaymentProviderOptions
{
    public MockPaymentProviderOptions(
        TimeSpan timeoutSimulationDelay)
    {
        if (timeoutSimulationDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutSimulationDelay),
                timeoutSimulationDelay,
                "Timeout simulation delay must be greater than zero.");
        }

        TimeoutSimulationDelay =
            timeoutSimulationDelay;
    }

    public TimeSpan TimeoutSimulationDelay { get; }
}
