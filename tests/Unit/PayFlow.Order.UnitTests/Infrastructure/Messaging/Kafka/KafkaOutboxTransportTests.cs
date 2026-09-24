using Confluent.Kafka;
using PayFlow.Order.Infrastructure.Messaging.Kafka;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.UnitTests.Infrastructure.Messaging.Kafka;

public sealed class KafkaOutboxTransportTests
{
    private static readonly Guid MessageId =
        Guid.Parse("8aeedade-ed7c-4c09-b51c-68481d424d76");

    private static readonly Guid OrderId =
        Guid.Parse("4cab3fe5-c9f6-48f5-a0a1-aab066aa4c6f");

    private static readonly Guid CausationId =
        Guid.Parse("5b84321e-3ce0-44ec-8855-c98e4750ff18");

    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 25, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapperCreatesKafkaKeyPayloadAndHeaders()
    {
        var entity = CreateMessage(
            CausationId,
            "00-test-traceparent");

        var actual =
            KafkaOutboxMessageMapper.Map(entity);

        Assert.Equal("orders.events", actual.Topic);
        Assert.Equal(OrderId.ToString("D"), actual.Key);
        Assert.Equal("{"orderId":"test"}", actual.Value);
        Assert.Equal(
            MessageId.ToString("D"),
            GetHeader(actual, "message-id"));
        Assert.Equal(
            "OrderCreated.v1",
            GetHeader(actual, "message-type"));
        Assert.Equal(
            "1",
            GetHeader(actual, "schema-version"));
        Assert.Equal(
            OrderId.ToString("D"),
            GetHeader(actual, "aggregate-id"));
        Assert.Equal(
            OrderId.ToString("D"),
            GetHeader(actual, "correlation-id"));
        Assert.Equal(
            CausationId.ToString("D"),
            GetHeader(actual, "causation-id"));
        Assert.Equal(
            OccurredAtUtc.ToString("O"),
            GetHeader(actual, "occurred-at-utc"));
        Assert.Equal(
            "Order",
            GetHeader(actual, "producer"));
        Assert.Equal(
            "00-test-traceparent",
            GetHeader(actual, "traceparent"));
    }

    [Fact]
    public void MapperOmitsOptionalHeadersWhenAbsent()
    {
        var actual = KafkaOutboxMessageMapper.Map(
            CreateMessage(
                causationId: null,
                traceParent: null));

        Assert.DoesNotContain(
            actual.Headers,
            header => header.Name == "causation-id");
        Assert.DoesNotContain(
            actual.Headers,
            header => header.Name == "traceparent");
    }

    [Fact]
    public async Task TransportPassesMappedRequestToProducer()
    {
        var producer = new FakeKafkaMessageProducer();
        var transport =
            new KafkaOutboxTransport(producer);
        var entity = CreateMessage(
            CausationId,
            "00-test-traceparent");

        await transport.PublishAsync(
            entity,
            TestContext.Current.CancellationToken);

        var request = Assert.IsType<KafkaPublishRequest>(
            producer.PublishedRequest);

        Assert.Equal(entity.Destination, request.Topic);
        Assert.Equal(
            entity.AggregateId.ToString("D"),
            request.Key);
        Assert.Equal(entity.Payload, request.Value);
    }

    [Fact]
    public void ProducerConfigEnablesIdempotentAllAckDelivery()
    {
        var options = new KafkaProducerOptions(
            "localhost:9092",
            "payflow-order-test",
            TimeSpan.FromSeconds(10));

        var actual =
            KafkaProducerConfigFactory.Create(options);

        Assert.Equal(
            "localhost:9092",
            actual.BootstrapServers);
        Assert.Equal(
            "payflow-order-test",
            actual.ClientId);
        Assert.True(actual.EnableIdempotence);
        Assert.Equal(Acks.All, actual.Acks);
        Assert.Equal(10_000, actual.MessageTimeoutMs);
    }

    private static OutboxMessageEntity CreateMessage(
        Guid? causationId,
        string? traceParent)
    {
        return new OutboxMessageEntity
        {
            OutboxMessageId = Guid.NewGuid(),
            MessageId = MessageId,
            MessageType = "OrderCreated.v1",
            SchemaVersion = 1,
            AggregateId = OrderId,
            CorrelationId = OrderId,
            CausationId = causationId,
            OccurredAtUtc = OccurredAtUtc,
            Destination = "orders.events",
            Producer = "Order",
            TraceParent = traceParent,
            Payload = "{"orderId":"test"}",
            CreatedAtUtc = OccurredAtUtc
        };
    }

    private static string GetHeader(
        KafkaPublishRequest request,
        string name)
    {
        return Assert.Single(
            request.Headers,
            header => header.Name == name)
            .Value;
    }

    private sealed class FakeKafkaMessageProducer
        : IKafkaMessageProducer
    {
        public KafkaPublishRequest? PublishedRequest
        {
            get;
            private set;
        }

        public Task PublishAsync(
            KafkaPublishRequest request,
            CancellationToken cancellationToken = default)
        {
            PublishedRequest = request;
            return Task.CompletedTask;
        }
    }
}
