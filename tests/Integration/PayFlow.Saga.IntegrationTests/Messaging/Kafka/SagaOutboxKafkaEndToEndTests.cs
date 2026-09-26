using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Kafka;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging.Kafka;

public sealed class SagaOutboxKafkaEndToEndTests(
    PostgreSqlFixture postgreSqlFixture,
    KafkaFixture kafkaFixture)
    : IClassFixture<PostgreSqlFixture>,
      IClassFixture<KafkaFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OrderCreatedPublishesBeginOrderProcessingAndMarksOutboxPublished()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var incomingMessageId = Guid.NewGuid();

        var consumedMessage =
            new ConsumedOrderCreatedMessage(
                new OrderCreatedMessage(
                    new IntegrationMessageEnvelope(
                        incomingMessageId,
                        "OrderCreated.v1",
                        1,
                        orderId,
                        orderId,
                        null,
                        OccurredAtUtc,
                        "Order",
                        null),
                    new OrderCreatedV1(
                        orderId,
                        Guid.NewGuid(),
                        "USD",
                        25m,
                        [
                            new OrderCreatedItemV1(
                                "SKU-SAGA-KAFKA-E2E",
                                1,
                                25m)
                        ])),
                "orders.events",
                0,
                300,
                OccurredAtUtc.AddSeconds(1));

        await using (var processDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            var handler =
                new OrderCreatedMessageHandler(
                    new CheckoutSagaRepository(
                        processDbContext),
                    new SagaEfUnitOfWork(
                        processDbContext),
                    new SagaOutboxWriter(
                        processDbContext),
                    TimeSpan.FromMinutes(30));

            var processor =
                new OrderCreatedInboxProcessor(
                    processDbContext,
                    new InboxMessageRepository(
                        processDbContext),
                    handler);

            Assert.True(
                await processor.ProcessAsync(
                    consumedMessage,
                    OccurredAtUtc.AddSeconds(2),
                    cancellationToken));
        }

        var producerConfig =
            KafkaProducerConfigFactory.Create(
                new KafkaProducerOptions(
                    kafkaFixture.BootstrapServers,
                    "payflow-saga-e2e-producer",
                    TimeSpan.FromSeconds(10)));

        using var kafkaProducer =
            new ProducerBuilder<string, string>(
                producerConfig)
            .Build();

        await using (var publishDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            var publisher =
                new OutboxPublisher(
                    new OutboxMessageRepository(
                        publishDbContext),
                    new KafkaOutboxTransport(
                        new ConfluentKafkaMessageProducer(
                            kafkaProducer)),
                    TimeProvider.System,
                    new OutboxPublisherOptions(
                        batchSize: 10,
                        leaseDuration:
                            TimeSpan.FromSeconds(30),
                        baseRetryDelay:
                            TimeSpan.FromSeconds(5),
                        maxRetryDelay:
                            TimeSpan.FromMinutes(1)));

            var publishResult =
                await publisher.PublishBatchAsync(
                    cancellationToken);

            Assert.Equal(
                new OutboxPublishBatchResult(
                    ClaimedCount: 1,
                    PublishedCount: 1,
                    FailedCount: 0),
                publishResult);
        }

        var consumerConfig =
            new ConsumerConfig
            {
                BootstrapServers =
                    kafkaFixture.BootstrapServers,
                GroupId =
                    $"payflow-saga-e2e-{Guid.NewGuid():N}",
                AutoOffsetReset =
                    AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

        using var consumer =
            new ConsumerBuilder<string, string>(
                consumerConfig)
            .Build();

        consumer.Subscribe(
            KafkaFixture.OrdersCommandsTopic);

        var consumed =
            consumer.Consume(
                TimeSpan.FromSeconds(15));

        Assert.NotNull(consumed);
        Assert.Equal(
            orderId.ToString("D"),
            consumed.Message.Key);

        using var payload =
            JsonDocument.Parse(
                consumed.Message.Value);

        Assert.Equal(
            orderId,
            payload.RootElement
                .GetProperty("orderId")
                .GetGuid());

        Assert.Equal(
            "BeginOrderProcessing.v1",
            GetHeader(
                consumed.Message.Headers,
                "message-type"));
        Assert.Equal(
            "1",
            GetHeader(
                consumed.Message.Headers,
                "schema-version"));
        Assert.Equal(
            orderId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "aggregate-id"));
        Assert.Equal(
            orderId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "correlation-id"));
        Assert.Equal(
            incomingMessageId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "causation-id"));
        Assert.Equal(
            "Saga",
            GetHeader(
                consumed.Message.Headers,
                "producer"));

        await using var verificationDbContext =
            postgreSqlFixture.CreateDbContext();

        var outboxMessage =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId,
                    cancellationToken);

        Assert.NotNull(
            outboxMessage.PublishedAtUtc);
        Assert.Equal(
            1,
            outboxMessage.AttemptCount);
        Assert.Null(
            outboxMessage.ClaimToken);
        Assert.Null(
            outboxMessage.ClaimedUntilUtc);
        Assert.Equal(
            outboxMessage.MessageId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "message-id"));
    }

    private static string GetHeader(
        Headers headers,
        string name)
    {
        var value =
            headers.GetLastBytes(name);

        Assert.NotNull(value);

        return Encoding.UTF8.GetString(value);
    }
}
