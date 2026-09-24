using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Infrastructure.Messaging.Kafka;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.Infrastructure.Time;
using PayFlow.Order.IntegrationTests.Infrastructure;

namespace PayFlow.Order.IntegrationTests.Messaging.Kafka;

public sealed class OrderOutboxKafkaEndToEndTests(
    PostgreSqlFixture postgreSqlFixture,
    KafkaFixture kafkaFixture)
    : IClassFixture<PostgreSqlFixture>,
      IClassFixture<KafkaFixture>
{
    [Fact]
    public async Task CreateOrderPublishesOrderCreatedToKafkaAndMarksOutboxPublished()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var customerId = Guid.NewGuid();
        var idempotencyKey =
            $"create-order-kafka-e2e-{Guid.NewGuid():N}";
        var command = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-KAFKA-E2E",
                    2,
                    17.50m,
                    "USD")
            ]);

        CreateOrderResult result;

        await using (var createDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            var handler = new CreateOrderHandler(
                new OrderRepository(createDbContext),
                new CreateOrderIdempotencyRepository(
                    createDbContext),
                new OrderOutboxWriter(createDbContext),
                new EfUnitOfWork(createDbContext),
                new SystemClock());

            result = await handler.HandleAsync(
                command,
                idempotencyKey,
                cancellationToken);
        }

        var producerConfig =
            KafkaProducerConfigFactory.Create(
                new KafkaProducerOptions(
                    kafkaFixture.BootstrapServers,
                    "payflow-order-e2e-producer",
                    TimeSpan.FromSeconds(10)));

        using var kafkaProducer =
            new ProducerBuilder<string, string>(
                producerConfig)
            .Build();

        await using (var publishDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            var publisher = new OutboxPublisher(
                new OutboxMessageRepository(
                    publishDbContext),
                new KafkaOutboxTransport(
                    new ConfluentKafkaMessageProducer(
                        kafkaProducer)),
                new SystemClock(),
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

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers =
                kafkaFixture.BootstrapServers,
            GroupId =
                $"payflow-order-e2e-{Guid.NewGuid():N}",
            AutoOffsetReset =
                AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer =
            new ConsumerBuilder<string, string>(
                consumerConfig)
            .Build();

        consumer.Subscribe(
            KafkaFixture.OrdersEventsTopic);

        var consumed = consumer.Consume(
            TimeSpan.FromSeconds(15));

        Assert.NotNull(consumed);
        Assert.Equal(
            result.OrderId.ToString("D"),
            consumed.Message.Key);

        using var payload =
            JsonDocument.Parse(
                consumed.Message.Value);

        Assert.Equal(
            result.OrderId,
            payload.RootElement
                .GetProperty("orderId")
                .GetGuid());
        Assert.Equal(
            customerId,
            payload.RootElement
                .GetProperty("customerId")
                .GetGuid());

        Assert.Equal(
            "OrderCreated.v1",
            GetHeader(
                consumed.Message.Headers,
                "message-type"));
        Assert.Equal(
            "1",
            GetHeader(
                consumed.Message.Headers,
                "schema-version"));
        Assert.Equal(
            result.OrderId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "aggregate-id"));
        Assert.Equal(
            result.OrderId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "correlation-id"));

        await using var verificationDbContext =
            postgreSqlFixture.CreateDbContext();

        var outboxMessage =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId
                            == result.OrderId,
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
        var value = headers.GetLastBytes(name);

        Assert.NotNull(value);

        return Encoding.UTF8.GetString(value);
    }
}
