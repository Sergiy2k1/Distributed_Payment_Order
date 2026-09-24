namespace PayFlow.Order.Infrastructure.Messaging.Kafka;

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
                nameof(messageTimeout),
                messageTimeout,
                "Kafka message timeout must be greater than zero.");
        }

        if (messageTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageTimeout),
                messageTimeout,
                "Kafka message timeout is too large.");
        }

        BootstrapServers = bootstrapServers;
        ClientId = clientId;
        MessageTimeout = messageTimeout;
    }

    public string BootstrapServers { get; }

    public string ClientId { get; }

    public TimeSpan MessageTimeout { get; }
}
