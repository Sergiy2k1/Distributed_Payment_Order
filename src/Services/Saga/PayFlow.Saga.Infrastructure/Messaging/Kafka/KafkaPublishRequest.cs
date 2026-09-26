namespace PayFlow.Saga.Infrastructure.Messaging.Kafka;

public sealed record KafkaPublishRequest(
    string Topic,
    string Key,
    string Value,
    IReadOnlyList<KafkaHeader> Headers);
