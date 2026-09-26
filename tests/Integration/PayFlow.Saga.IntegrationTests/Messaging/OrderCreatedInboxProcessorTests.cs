using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class OrderCreatedInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAtUtc =
        OccurredAtUtc.AddSeconds(1);

    private static readonly DateTimeOffset ProcessedAtUtc =
        OccurredAtUtc.AddSeconds(2);

    [Fact]
    public async Task FirstDeliveryInvokesHandlerAndMarksInboxProcessed()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var consumedMessage =
            CreateConsumedMessage(Guid.NewGuid());
        var handler = new RecordingHandler();

        await using var dbContext =
            fixture.CreateDbContext();
        var processor = new OrderCreatedInboxProcessor(
            dbContext,
            new InboxMessageRepository(dbContext),
            handler);

        var processed = await processor.ProcessAsync(
            consumedMessage,
            ProcessedAtUtc,
            cancellationToken);

        Assert.True(processed);
        Assert.Equal(1, handler.CallCount);

        var inbox =
            await dbContext.InboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.MessageId
                            == consumedMessage
                                .Message
                                .Envelope
                                .MessageId,
                    cancellationToken);

        Assert.Equal(
            ProcessedAtUtc,
            inbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task DuplicateDeliveryDoesNotInvokeHandlerAgain()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var consumedMessage =
            CreateConsumedMessage(Guid.NewGuid());
        var handler = new RecordingHandler();

        await using (var firstDbContext =
            fixture.CreateDbContext())
        {
            var firstProcessor =
                new OrderCreatedInboxProcessor(
                    firstDbContext,
                    new InboxMessageRepository(
                        firstDbContext),
                    handler);

            Assert.True(
                await firstProcessor.ProcessAsync(
                    consumedMessage,
                    ProcessedAtUtc,
                    cancellationToken));
        }

        await using (var secondDbContext =
            fixture.CreateDbContext())
        {
            var secondProcessor =
                new OrderCreatedInboxProcessor(
                    secondDbContext,
                    new InboxMessageRepository(
                        secondDbContext),
                    handler);

            Assert.False(
                await secondProcessor.ProcessAsync(
                    consumedMessage with
                    {
                        SourceOffset =
                            consumedMessage.SourceOffset + 1
                    },
                    ProcessedAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task HandlerFailureRollsBackInboxSoRedeliveryCanRetry()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var consumedMessage =
            CreateConsumedMessage(Guid.NewGuid());

        await using (var failingDbContext =
            fixture.CreateDbContext())
        {
            var failingProcessor =
                new OrderCreatedInboxProcessor(
                    failingDbContext,
                    new InboxMessageRepository(
                        failingDbContext),
                    new ThrowingHandler());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => failingProcessor.ProcessAsync(
                    consumedMessage,
                    ProcessedAtUtc,
                    cancellationToken));
        }

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var count =
                await verificationDbContext
                    .InboxMessages
                    .AsNoTracking()
                    .CountAsync(
                        message =>
                            message.MessageId
                                == consumedMessage
                                    .Message
                                    .Envelope
                                    .MessageId,
                        cancellationToken);

            Assert.Equal(0, count);
        }

        var retryHandler = new RecordingHandler();

        await using var retryDbContext =
            fixture.CreateDbContext();
        var retryProcessor =
            new OrderCreatedInboxProcessor(
                retryDbContext,
                new InboxMessageRepository(
                    retryDbContext),
                retryHandler);

        Assert.True(
            await retryProcessor.ProcessAsync(
                consumedMessage,
                ProcessedAtUtc.AddSeconds(1),
                cancellationToken));

        Assert.Equal(1, retryHandler.CallCount);
    }

    private static ConsumedOrderCreatedMessage
        CreateConsumedMessage(
            Guid messageId)
    {
        var orderId = Guid.NewGuid();

        return new ConsumedOrderCreatedMessage(
            new OrderCreatedMessage(
                new IntegrationMessageEnvelope(
                    messageId,
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
                            35m)
                    ])),
            "orders.events",
            0,
            100,
            ReceivedAtUtc);
    }

    private sealed class RecordingHandler
        : IOrderCreatedMessageHandler
    {
        public int CallCount { get; private set; }

        public Task HandleAsync(
            OrderCreatedMessage message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler
        : IOrderCreatedMessageHandler
    {
        public Task HandleAsync(
            OrderCreatedMessage message,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated Saga handler failure.");
        }
    }
}
