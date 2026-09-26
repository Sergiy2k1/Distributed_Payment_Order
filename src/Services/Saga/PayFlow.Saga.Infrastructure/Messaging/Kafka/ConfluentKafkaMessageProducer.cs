using System.Text;
using Confluent.Kafka;

namespace PayFlow.Saga.Infrastructure.Messaging.Kafka;

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

        var message = new Message<string, string>
        {
            Key = request.Key,
            Value = request.Value,
            Headers = headers
        };

        var deliveryResult = await _producer
            .ProduceAsync(
                request.Topic,
                message,
                cancellationToken)
            .ConfigureAwait(false);

        if (deliveryResult.Status
            != PersistenceStatus.Persisted)
        {
            throw new InvalidOperationException(
                $"Kafka delivery was not confirmed as persisted. Status: {deliveryResult.Status}.");
        }
    }
}
