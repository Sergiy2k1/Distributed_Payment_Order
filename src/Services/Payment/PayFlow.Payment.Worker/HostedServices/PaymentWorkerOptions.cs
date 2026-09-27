namespace PayFlow.Payment.Worker.HostedServices;

public sealed class PaymentWorkerOptions
{
    public PaymentWorkerOptions(
        string bootstrapServers,
        string consumerGroup,
        TimeSpan consumeErrorDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            bootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            consumerGroup);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            consumeErrorDelay,
            TimeSpan.Zero);

        BootstrapServers = bootstrapServers;
        ConsumerGroup = consumerGroup;
        ConsumeErrorDelay = consumeErrorDelay;
    }

    public string BootstrapServers { get; }
    public string ConsumerGroup { get; }
    public TimeSpan ConsumeErrorDelay { get; }
}
