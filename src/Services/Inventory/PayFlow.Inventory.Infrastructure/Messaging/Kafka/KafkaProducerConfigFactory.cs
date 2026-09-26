using Confluent.Kafka;

namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public static class KafkaProducerConfigFactory
{
    public static ProducerConfig Create(
        KafkaProducerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = options.ClientId,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = checked(
                (int)options.MessageTimeout.TotalMilliseconds)
        };
    }
}
