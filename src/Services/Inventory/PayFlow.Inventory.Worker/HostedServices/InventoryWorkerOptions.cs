namespace PayFlow.Inventory.Worker.HostedServices;

public sealed class InventoryWorkerOptions
{
    public InventoryWorkerOptions(
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
