using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Kafka;

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
