using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Application.Messaging;
using PayFlow.Inventory.Application.Reservations;
using PayFlow.Inventory.Domain.Stock;
using PayFlow.Inventory.Infrastructure.Messaging;
using PayFlow.Inventory.Infrastructure.Messaging.Kafka;
using PayFlow.Inventory.Infrastructure.Messaging.Outbox;
using PayFlow.Inventory.Infrastructure.Persistence;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;
using PayFlow.Inventory.IntegrationTests.Infrastructure;

namespace PayFlow.Inventory.IntegrationTests.Messaging.Kafka;

public sealed class InventoryOutboxKafkaEndToEndTests(
    PostgreSqlFixture postgreSqlFixture,
    KafkaFixture kafkaFixture)
    : IClassFixture<PostgreSqlFixture>,
      IClassFixture<KafkaFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReserveInventoryPublishesInventoryReservedAndMarksOutboxPublished()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var skuId = $"SKU-KAFKA-{suffix}";
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await using (var seedDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            await new StockRepository(seedDbContext)
                .AddAsync(
                    StockItem.Create(
                        skuId,
                        5),
                    cancellationToken);

            await seedDbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using (var processDbContext =
            postgreSqlFixture.CreateDbContext())
        {
            var processor =
                new ReserveInventoryInboxProcessor(
                    processDbContext,
                    new InboxMessageRepository(
                        processDbContext),
                    new ReserveInventoryMessageHandler(
                        new InventoryReservationRepository(
                            processDbContext),
                        new StockRepository(
                            processDbContext),
                        new InventoryOutboxWriter(
                            processDbContext),
                        new InventoryEfUnitOfWork(
                            processDbContext)));

            var consumedMessage =
                new ConsumedReserveInventoryMessage(
                    new ReserveInventoryMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "ReserveInventory.v1",
                            1,
                            orderId,
                            orderId,
                            null,
                            OccurredAtUtc,
                            "Saga",
                            null),
                        new ReserveInventoryV1(
                            orderId,
                            reservationId,
                            [
                                new ReserveInventoryItemV1(
                                    skuId,
                                    2)
                            ],
                            OccurredAtUtc.AddMinutes(30))),
                    "inventory.commands",
                    0,
                    500,
                    OccurredAtUtc);

            Assert.True(
                await processor.ProcessAsync(
                    consumedMessage,
                    OccurredAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        var producerConfig =
            KafkaProducerConfigFactory.Create(
                new KafkaProducerOptions(
                    kafkaFixture.BootstrapServers,
                    "payflow-inventory-e2e-producer",
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

            var result =
                await publisher.PublishBatchAsync(
                    cancellationToken);

            Assert.Equal(1, result.ClaimedCount);
            Assert.Equal(1, result.PublishedCount);
            Assert.Equal(0, result.FailedCount);
        }

        using var consumer =
            new ConsumerBuilder<string, string>(
                new ConsumerConfig
                {
                    BootstrapServers =
                        kafkaFixture.BootstrapServers,
                    GroupId =
                        $"payflow-inventory-e2e-{Guid.NewGuid():N}",
                    AutoOffsetReset =
                        AutoOffsetReset.Earliest,
                    EnableAutoCommit = false
                })
            .Build();

        consumer.Subscribe(
            KafkaFixture.InventoryEventsTopic);

        var consumed =
            consumer.Consume(
                TimeSpan.FromSeconds(15));

        Assert.NotNull(consumed);
        Assert.Equal(
            orderId.ToString("D"),
            consumed.Message.Key);
        Assert.Equal(
            "InventoryReserved.v1",
            GetHeader(
                consumed.Message.Headers,
                "message-type"));
        Assert.Equal(
            orderId.ToString("D"),
            GetHeader(
                consumed.Message.Headers,
                "aggregate-id"));
        Assert.Equal(
            "Inventory",
            GetHeader(
                consumed.Message.Headers,
                "producer"));

        await using var verificationDbContext =
            postgreSqlFixture.CreateDbContext();

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId,
                    cancellationToken);

        Assert.NotNull(
            outbox.PublishedAtUtc);
        Assert.Equal(
            1,
            outbox.AttemptCount);
        Assert.Null(
            outbox.ClaimToken);
        Assert.Null(
            outbox.ClaimedUntilUtc);
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
