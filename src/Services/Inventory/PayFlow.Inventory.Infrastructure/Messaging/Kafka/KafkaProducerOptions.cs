namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public sealed class KafkaProducerOptions
{
    public KafkaProducerOptions(
        string bootstrapServers,
        string clientId,
        TimeSpan messageTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            bootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            clientId);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            messageTimeout,
            TimeSpan.Zero);

        BootstrapServers = bootstrapServers;
        ClientId = clientId;
        MessageTimeout = messageTimeout;
    }

    public string BootstrapServers { get; }
    public string ClientId { get; }
    public TimeSpan MessageTimeout { get; }
}
