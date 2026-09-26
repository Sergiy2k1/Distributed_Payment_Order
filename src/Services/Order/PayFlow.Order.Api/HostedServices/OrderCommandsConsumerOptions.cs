namespace PayFlow.Order.Api.HostedServices;

public sealed class OrderCommandsConsumerOptions
{
    public OrderCommandsConsumerOptions(
        string bootstrapServers,
        string consumerGroup,
        TimeSpan consumeErrorDelay)
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

        BootstrapServers = bootstrapServers;
        ConsumerGroup = consumerGroup;
        ConsumeErrorDelay = consumeErrorDelay;
    }

    public string BootstrapServers { get; }

    public string ConsumerGroup { get; }

    public TimeSpan ConsumeErrorDelay { get; }
}
