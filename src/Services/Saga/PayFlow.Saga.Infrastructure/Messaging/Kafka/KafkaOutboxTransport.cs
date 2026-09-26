using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Messaging.Kafka;

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
        var request =
            KafkaOutboxMessageMapper.Map(message);

        return _producer.PublishAsync(
            request,
            cancellationToken);
    }
}
