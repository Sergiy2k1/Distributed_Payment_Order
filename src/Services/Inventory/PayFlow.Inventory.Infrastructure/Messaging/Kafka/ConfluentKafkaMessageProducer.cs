using System.Text;
using Confluent.Kafka;

namespace PayFlow.Inventory.Infrastructure.Messaging.Kafka;

public sealed class ConfluentKafkaMessageProducer
    : IKafkaMessageProducer
{
    private readonly IProducer<string, string> _producer;

    public ConfluentKafkaMessageProducer(
        IProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task PublishAsync(
        KafkaPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var headers = new Headers();

        foreach (var header in request.Headers)
        {
            headers.Add(
                header.Name,
                Encoding.UTF8.GetBytes(header.Value));
        }

        var result = await _producer.ProduceAsync(
            request.Topic,
            new Message<string, string>
            {
                Key = request.Key,
                Value = request.Value,
                Headers = headers
            },
            cancellationToken);

        if (result.Status != PersistenceStatus.Persisted)
        {
            throw new InvalidOperationException(
                $"Kafka delivery was not persisted. Status: {result.Status}.");
        }
    }
}
