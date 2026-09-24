using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace PayFlow.Order.IntegrationTests.Infrastructure;

public sealed class KafkaFixture : IAsyncLifetime
{
    public const string OrdersEventsTopic = "orders.events";

    private readonly KafkaContainer _container =
        new KafkaBuilder("apache/kafka:4.3.1")
            .Build();

    public string BootstrapServers =>
        _container.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await _container
            .StartAsync(cancellationToken)
            .ConfigureAwait(false);

        using var adminClient =
            new AdminClientBuilder(
                new AdminClientConfig
                {
                    BootstrapServers = BootstrapServers
                })
            .Build();

        await adminClient
            .CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = OrdersEventsTopic,
                        NumPartitions = 3,
                        ReplicationFactor = 1
                    }
                ])
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _container
            .DisposeAsync()
            .ConfigureAwait(false);
    }
}
