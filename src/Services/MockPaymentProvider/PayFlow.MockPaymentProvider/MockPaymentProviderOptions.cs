namespace PayFlow.MockPaymentProvider;

public sealed class MockPaymentProviderOptions
{
    public MockPaymentProviderOptions(
        TimeSpan timeoutSimulationDelay)
        : this(
            timeoutSimulationDelay,
            new Uri("http://localhost:8086/provider/webhooks"),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100))
    {
    }

    public MockPaymentProviderOptions(
        TimeSpan timeoutSimulationDelay,
        Uri paymentWebhookEndpoint,
        TimeSpan delayedWebhookDelay,
        TimeSpan duplicateWebhookDelay)
    {
        if (timeoutSimulationDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutSimulationDelay),
                timeoutSimulationDelay,
                "Timeout simulation delay must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(paymentWebhookEndpoint);

        if (!paymentWebhookEndpoint.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "Payment webhook endpoint must be an absolute URI.",
                nameof(paymentWebhookEndpoint));
        }

        if (delayedWebhookDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayedWebhookDelay),
                delayedWebhookDelay,
                "Delayed webhook delay must be greater than zero.");
        }

        if (duplicateWebhookDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duplicateWebhookDelay),
                duplicateWebhookDelay,
                "Duplicate webhook delay cannot be negative.");
        }

        TimeoutSimulationDelay = timeoutSimulationDelay;
        PaymentWebhookEndpoint = paymentWebhookEndpoint;
        DelayedWebhookDelay = delayedWebhookDelay;
        DuplicateWebhookDelay = duplicateWebhookDelay;
    }

    public TimeSpan TimeoutSimulationDelay { get; }

    public Uri PaymentWebhookEndpoint { get; }

    public TimeSpan DelayedWebhookDelay { get; }

    public TimeSpan DuplicateWebhookDelay { get; }
}
