namespace PayFlow.Saga.Worker.HostedServices;

public sealed class OrderCreatedConsumerOptions
{
    public OrderCreatedConsumerOptions(
        string bootstrapServers,
        string consumerGroup,
        TimeSpan consumeErrorDelay,
        TimeSpan checkoutTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            bootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            consumerGroup);

        if (consumeErrorDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consumeErrorDelay),
                consumeErrorDelay,
                "Consume error delay must be greater than zero.");
        }

        if (checkoutTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkoutTimeout),
                checkoutTimeout,
                "Checkout timeout must be greater than zero.");
        }

        BootstrapServers = bootstrapServers;
        ConsumerGroup = consumerGroup;
        ConsumeErrorDelay = consumeErrorDelay;
        CheckoutTimeout = checkoutTimeout;
    }

    public string BootstrapServers { get; }

    public string ConsumerGroup { get; }

    public TimeSpan ConsumeErrorDelay { get; }

    public TimeSpan CheckoutTimeout { get; }
}
