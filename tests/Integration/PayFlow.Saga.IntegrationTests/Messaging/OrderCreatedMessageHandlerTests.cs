using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Entities;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class OrderCreatedMessageHandlerTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAtUtc =
        OccurredAtUtc.AddSeconds(1);

    private static readonly DateTimeOffset ProcessedAtUtc =
        OccurredAtUtc.AddSeconds(2);

    private static readonly TimeSpan CheckoutTimeout =
        TimeSpan.FromMinutes(30);

    [Fact]
    public async Task ProcessingOrderCreatedPersistsSagaAndInboxAtomically()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var consumedMessage =
            CreateConsumedMessage();

        await using var dbContext =
            fixture.CreateDbContext();

        var handler =
            new OrderCreatedMessageHandler(
                new CheckoutSagaRepository(dbContext),
                new SagaEfUnitOfWork(dbContext),
                new SagaOutboxWriter(dbContext),
                CheckoutTimeout);

        var processor =
            new OrderCreatedInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                handler);

        var processed =
            await processor.ProcessAsync(
                consumedMessage,
                ProcessedAtUtc,
                cancellationToken);

        Assert.True(processed);

        var persistedSaga =
            await dbContext.CheckoutSagas
                .AsNoTracking()
                .Include(saga => saga.Items)
                .SingleAsync(
                    saga =>
                        saga.OrderId
                        == consumedMessage.Message.Payload.OrderId,
                    cancellationToken);

        Assert.Equal(
            consumedMessage.Message.Payload.CustomerId,
            persistedSaga.CustomerId);
        Assert.Equal(
            CheckoutSagaStatus.Started.ToString(),
            persistedSaga.Status);
        Assert.Equal(
            "USD",
            persistedSaga.Currency);
        Assert.Equal(
            35m,
            persistedSaga.TotalAmount);
        Assert.Equal(
            OccurredAtUtc,
            persistedSaga.StartedAtUtc);
        Assert.Equal(
            OccurredAtUtc,
            persistedSaga.UpdatedAtUtc);
        Assert.Equal(
            OccurredAtUtc.Add(CheckoutTimeout),
            persistedSaga.DeadlineAtUtc);
        Assert.Equal(
            0,
            persistedSaga.RetryCount);
        Assert.Null(
            persistedSaga.NextAttemptAtUtc);
        Assert.Equal(
            0,
            persistedSaga.Version);

        var persistedItems =
            persistedSaga.Items
                .OrderBy(item => item.Position)
                .ToArray();

        Assert.Equal(
            2,
            persistedItems.Length);
        Assert.Equal(
            "SKU-001",
            persistedItems[0].SkuId);
        Assert.Equal(
            1,
            persistedItems[0].Quantity);
        Assert.Equal(
            15m,
            persistedItems[0].UnitPrice);
        Assert.Equal(
            "SKU-002",
            persistedItems[1].SkuId);
        Assert.Equal(
            2,
            persistedItems[1].Quantity);
        Assert.Equal(
            10m,
            persistedItems[1].UnitPrice);

        var inbox =
            await dbContext.InboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.ConsumerName
                            == OrderCreatedInboxProcessor.ConsumerName
                        && message.MessageId
                            == consumedMessage.Message.Envelope.MessageId,
                    cancellationToken);

        Assert.Equal(
            ProcessedAtUtc,
            inbox.ProcessedAtUtc);

        var outboxMessage =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId
                        == consumedMessage.Message.Payload.OrderId,
                    cancellationToken);

        Assert.Equal(
            "BeginOrderProcessing.v1",
            outboxMessage.MessageType);
        Assert.Equal(
            1,
            outboxMessage.SchemaVersion);
        Assert.Equal(
            consumedMessage.Message.Payload.OrderId,
            outboxMessage.AggregateId);
        Assert.Equal(
            consumedMessage.Message.Envelope.CorrelationId,
            outboxMessage.CorrelationId);
        Assert.Equal(
            consumedMessage.Message.Envelope.MessageId,
            outboxMessage.CausationId);
        Assert.Equal(
            OccurredAtUtc,
            outboxMessage.OccurredAtUtc);
        Assert.Equal(
            "orders.commands",
            outboxMessage.Destination);
        Assert.Equal(
            "Saga",
            outboxMessage.Producer);
        Assert.Null(
            outboxMessage.PublishedAtUtc);
        Assert.Equal(
            0,
            outboxMessage.AttemptCount);

        var payload =
            JsonSerializer.Deserialize<BeginOrderProcessingV1>(
                outboxMessage.Payload,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal(
            consumedMessage.Message.Payload.OrderId,
            payload.OrderId);
    }

    [Fact]
    public async Task FailureAfterSagaSaveRollsBackSagaAndInbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var consumedMessage =
            CreateConsumedMessage();

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var inboxRepository =
                new ThrowingMarkProcessedInboxRepository(
                    new InboxMessageRepository(dbContext));

            var handler =
                new OrderCreatedMessageHandler(
                    new CheckoutSagaRepository(dbContext),
                    new SagaEfUnitOfWork(dbContext),
                    new SagaOutboxWriter(dbContext),
                    CheckoutTimeout);

            var processor =
                new OrderCreatedInboxProcessor(
                    dbContext,
                    inboxRepository,
                    handler);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessAsync(
                    consumedMessage,
                    ProcessedAtUtc,
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var sagaCount =
            await verificationDbContext.CheckoutSagas
                .AsNoTracking()
                .CountAsync(
                    saga =>
                        saga.OrderId
                        == consumedMessage.Message.Payload.OrderId,
                    cancellationToken);

        var inboxCount =
            await verificationDbContext.InboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.ConsumerName
                            == OrderCreatedInboxProcessor.ConsumerName
                        && message.MessageId
                            == consumedMessage.Message.Envelope.MessageId,
                    cancellationToken);

        var outboxCount =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId
                        == consumedMessage.Message.Payload.OrderId,
                    cancellationToken);

        Assert.Equal(
            0,
            sagaCount);
        Assert.Equal(
            0,
            inboxCount);
        Assert.Equal(
            0,
            outboxCount);
    }

    private static ConsumedOrderCreatedMessage
        CreateConsumedMessage()
    {
        var orderId = Guid.NewGuid();

        return new ConsumedOrderCreatedMessage(
            new OrderCreatedMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
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
                    35m,
                    [
                        new OrderCreatedItemV1(
                            "SKU-001",
                            1,
                            15m),
                        new OrderCreatedItemV1(
                            "SKU-002",
                            2,
                            10m)
                    ])),
            "orders.events",
            0,
            200,
            ReceivedAtUtc);
    }

    private sealed class ThrowingMarkProcessedInboxRepository(
        IInboxMessageRepository inner)
        : IInboxMessageRepository
    {
        public Task<bool> TryInsertAsync(
            InboxMessageEntity message,
            CancellationToken cancellationToken = default)
        {
            return inner.TryInsertAsync(
                message,
                cancellationToken);
        }

        public Task MarkProcessedAsync(
            string consumerName,
            Guid messageId,
            DateTimeOffset processedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated failure after Saga persistence.");
        }
    }
}
