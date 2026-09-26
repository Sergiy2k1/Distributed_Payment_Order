using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Messaging;
using PayFlow.Order.Application.Orders.BeginOrderProcessing;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Messaging;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Entities;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.IntegrationTests.Infrastructure;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.IntegrationTests.Messaging;

public sealed class BeginOrderProcessingInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 16, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CommandOccurredAtUtc =
        CreatedAtUtc.AddSeconds(1);

    private static readonly DateTimeOffset ReceivedAtUtc =
        CommandOccurredAtUtc.AddSeconds(1);

    private static readonly DateTimeOffset ProcessedAtUtc =
        CommandOccurredAtUtc.AddSeconds(2);

    [Fact]
    public async Task ProcessingCommandTransitionsOrderAndPersistsInboxAndOutboxAtomically()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await SeedPendingOrderAsync(
            orderId,
            cancellationToken);

        var consumedMessage =
            CreateConsumedMessage(
                orderId,
                messageId,
                correlationId);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var orderRepository =
                new OrderRepository(dbContext);

            var handler =
                new BeginOrderProcessingMessageHandler(
                    orderRepository,
                    new OrderCommandOutboxWriter(dbContext),
                    new EfUnitOfWork(dbContext));

            var processor =
                new BeginOrderProcessingInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    handler);

            var processed =
                await processor.ProcessAsync(
                    consumedMessage,
                    ProcessedAtUtc,
                    cancellationToken);

            Assert.True(processed);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var order =
            await verificationDbContext.Orders
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.Id == orderId,
                    cancellationToken);

        Assert.Equal(
            OrderStatus.Processing.ToString(),
            order.Status);
        Assert.Equal(
            1,
            order.Version);
        Assert.Equal(
            CommandOccurredAtUtc,
            order.UpdatedAtUtc);

        var inbox =
            await verificationDbContext.InboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.ConsumerName
                            == BeginOrderProcessingInboxProcessor.ConsumerName
                        && message.MessageId == messageId,
                    cancellationToken);

        Assert.Equal(
            ProcessedAtUtc,
            inbox.ProcessedAtUtc);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                            == "OrderProcessingStarted.v1",
                    cancellationToken);

        Assert.Equal(
            correlationId,
            outbox.CorrelationId);
        Assert.Equal(
            messageId,
            outbox.CausationId);
        Assert.Equal(
            CommandOccurredAtUtc,
            outbox.OccurredAtUtc);
        Assert.Equal(
            "orders.events",
            outbox.Destination);
        Assert.Equal(
            "Order",
            outbox.Producer);

        using var payload =
            JsonDocument.Parse(outbox.Payload);

        Assert.Equal(
            orderId,
            payload.RootElement
                .GetProperty("orderId")
                .GetGuid());
    }

    [Fact]
    public async Task FailureAfterOrderSaveRollsBackOrderInboxAndOutbox()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await SeedPendingOrderAsync(
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var orderRepository =
                new OrderRepository(dbContext);

            var handler =
                new BeginOrderProcessingMessageHandler(
                    orderRepository,
                    new OrderCommandOutboxWriter(dbContext),
                    new EfUnitOfWork(dbContext));

            var inboxRepository =
                new ThrowingMarkProcessedInboxRepository(
                    new InboxMessageRepository(dbContext));

            var processor =
                new BeginOrderProcessingInboxProcessor(
                    dbContext,
                    inboxRepository,
                    handler);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessAsync(
                    CreateConsumedMessage(
                        orderId,
                        messageId,
                        Guid.NewGuid()),
                    ProcessedAtUtc,
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var order =
            await verificationDbContext.Orders
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.Id == orderId,
                    cancellationToken);

        Assert.Equal(
            OrderStatus.Pending.ToString(),
            order.Status);
        Assert.Equal(
            0,
            order.Version);

        Assert.Equal(
            0,
            await verificationDbContext.InboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.ConsumerName
                            == BeginOrderProcessingInboxProcessor.ConsumerName
                        && message.MessageId == messageId,
                    cancellationToken));

        Assert.Equal(
            0,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                            == "OrderProcessingStarted.v1",
                    cancellationToken));
    }

    private async Task SeedPendingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order =
            OrderAggregate.Create(
                OrderId.From(orderId),
                CustomerId.New(),
                [
                    OrderItem.Create(
                        Sku.From("SKU-COMMAND"),
                        1,
                        Money.From(25m, "USD"))
                ],
                CreatedAtUtc);

        order.ClearDomainEvents();

        await using var dbContext =
            fixture.CreateDbContext();

        await new OrderRepository(dbContext)
            .AddAsync(
                order,
                cancellationToken);

        await dbContext
            .SaveChangesAsync(cancellationToken);
    }

    private static ConsumedBeginOrderProcessingMessage
        CreateConsumedMessage(
            Guid orderId,
            Guid messageId,
            Guid correlationId)
    {
        return new ConsumedBeginOrderProcessingMessage(
            new BeginOrderProcessingMessage(
                new IntegrationMessageEnvelope(
                    messageId,
                    "BeginOrderProcessing.v1",
                    1,
                    orderId,
                    correlationId,
                    null,
                    CommandOccurredAtUtc,
                    "Saga",
                    null),
                new BeginOrderProcessingV1(
                    orderId)),
            "orders.commands",
            0,
            100,
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
                "Simulated failure after Order persistence.");
        }
    }
}
