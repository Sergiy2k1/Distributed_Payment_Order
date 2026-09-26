using PayFlow.Inventory.Infrastructure.Messaging.Outbox;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public sealed class KafkaOutboxTransport
    : IOutboxTransport
{
    private readonly IKafkaMessageProducer _producer;

    public KafkaOutboxTransport(
        IKafkaMessageProducer producer)
    {
        _producer = producer;
    }

    public Task PublishAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken = default)
    {
        return _producer.PublishAsync(
            KafkaOutboxMessageMapper.Map(message),
            cancellationToken);
    }
}
