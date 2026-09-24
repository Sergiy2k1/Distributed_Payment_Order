namespace PayFlow.Order.Infrastructure.Messaging.Kafka;

public interface IKafkaMessageProducer
{
    Task PublishAsync(
        KafkaPublishRequest request,
        CancellationToken cancellationToken = default);
}
