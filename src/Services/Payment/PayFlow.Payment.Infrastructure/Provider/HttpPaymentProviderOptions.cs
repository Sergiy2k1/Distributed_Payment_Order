namespace PayFlow.Payment.Infrastructure.Provider;

public sealed class HttpPaymentProviderOptions
{
    public HttpPaymentProviderOptions(
        Uri baseAddress,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            timeout,
            TimeSpan.Zero);

        BaseAddress = baseAddress;
        Timeout = timeout;
    }

    public Uri BaseAddress { get; }
    public TimeSpan Timeout { get; }
}
