using PayFlow.Payment.Infrastructure.Messaging.Outbox;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Messaging.Kafka;

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
