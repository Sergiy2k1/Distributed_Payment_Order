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

        if (messageTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageTimeout));
        }

        BootstrapServers = bootstrapServers;
        ClientId = clientId;
        MessageTimeout = messageTimeout;
    }

    public string BootstrapServers { get; }
    public string ClientId { get; }
    public TimeSpan MessageTimeout { get; }
}
