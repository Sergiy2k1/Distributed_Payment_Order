namespace PayFlow.Order.Infrastructure.Messaging.Kafka;

public sealed record KafkaHeader(
    string Name,
    string Value);
