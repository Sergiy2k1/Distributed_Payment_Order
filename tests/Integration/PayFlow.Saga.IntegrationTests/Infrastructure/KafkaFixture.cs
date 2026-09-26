using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;

namespace PayFlow.Saga.IntegrationTests.Infrastructure;

public sealed class KafkaFixture : IAsyncLifetime
{
    public const string OrdersCommandsTopic =
        "orders.commands";

    private readonly INetwork _network =
        new NetworkBuilder()
            .Build();

    private readonly KafkaContainer _container;

    public KafkaFixture()
    {
        _container =
            new KafkaBuilder("apache/kafka:4.3.1")
                .WithNetwork(_network)
                .WithListener("kafka:19092")
                .Build();
    }

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
                    BootstrapServers =
                        BootstrapServers
                })
            .Build();

        await adminClient
            .CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = OrdersCommandsTopic,
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

        await _network
            .DisposeAsync()
            .ConfigureAwait(false);
    }
}
