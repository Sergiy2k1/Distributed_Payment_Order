using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.UnitTests.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherTests
{
    private static readonly DateTimeOffset ClaimTimeUtc =
        new(2026, 9, 24, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublishBatchMarksSuccessfulMessagesPublished()
    {
        var first = CreateMessage(Guid.NewGuid());
        var second = CreateMessage(Guid.NewGuid());
        var repository = new FakeOutboxMessageRepository(
            [first, second]);
        var transport = new FakeOutboxTransport();
        var clock = new SequenceClock(
            ClaimTimeUtc,
            ClaimTimeUtc.AddSeconds(1),
            ClaimTimeUtc.AddSeconds(2));
        var publisher = CreatePublisher(
            repository,
            transport,
            clock);

        var result = await publisher.PublishBatchAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new OutboxPublishBatchResult(2, 2, 0),
            result);
        Assert.Equal(
            [first.OutboxMessageId, second.OutboxMessageId],
            transport.PublishedMessageIds);
        Assert.Equal(2, repository.Published.Count);
        Assert.Empty(repository.Failed);
    }

    [Fact]
    public async Task PublishBatchMarksFailureAndContinuesBatch()
    {
        var failedMessage = CreateMessage(
            Guid.NewGuid(),
            attemptCount: 2);
        var successfulMessage =
            CreateMessage(Guid.NewGuid());
        var repository = new FakeOutboxMessageRepository(
            [failedMessage, successfulMessage]);
        var transport = new FakeOutboxTransport(
            failedMessage.OutboxMessageId);
        var failedAtUtc =
            ClaimTimeUtc.AddSeconds(1);
        var clock = new SequenceClock(
            ClaimTimeUtc,
            failedAtUtc,
            ClaimTimeUtc.AddSeconds(2));
        var publisher = CreatePublisher(
            repository,
            transport,
            clock);

        var result = await publisher.PublishBatchAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new OutboxPublishBatchResult(2, 1, 1),
            result);

        var failure = Assert.Single(
            repository.Failed);
        Assert.Equal(
            failedMessage.OutboxMessageId,
            failure.OutboxMessageId);
        Assert.Equal(
            failedAtUtc.AddSeconds(20),
            failure.NextAttemptAtUtc);
        Assert.Equal(
            nameof(InvalidOperationException),
            failure.ErrorCode);

        var published = Assert.Single(
            repository.Published);
        Assert.Equal(
            successfulMessage.OutboxMessageId,
            published.OutboxMessageId);
    }

    [Fact]
    public async Task PublishBatchPropagatesRequestedCancellation()
    {
        var message = CreateMessage(Guid.NewGuid());
        var repository = new FakeOutboxMessageRepository(
            [message]);
        using var cancellationTokenSource =
            new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var transport = new CancellingOutboxTransport();
        var publisher = CreatePublisher(
            repository,
            transport,
            new SequenceClock(ClaimTimeUtc));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => publisher.PublishBatchAsync(
                cancellationTokenSource.Token));

        Assert.Empty(repository.Published);
        Assert.Empty(repository.Failed);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 60)]
    [InlineData(10, 60)]
    public void RetryPolicyUsesExponentialDelayWithCap(
        int previousAttemptCount,
        int expectedSeconds)
    {
        var policy = new OutboxRetryPolicy(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(60));

        var actual = policy.GetDelay(
            previousAttemptCount);

        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            actual);
    }

    private static OutboxPublisher CreatePublisher(
        IOutboxMessageRepository repository,
        IOutboxTransport transport,
        IClock clock)
    {
        return new OutboxPublisher(
            repository,
            transport,
            clock,
            new OutboxPublisherOptions(
                batchSize: 10,
                leaseDuration: TimeSpan.FromSeconds(30),
                baseRetryDelay: TimeSpan.FromSeconds(5),
                maxRetryDelay: TimeSpan.FromSeconds(60)));
    }

    private static OutboxMessageEntity CreateMessage(
        Guid outboxMessageId,
        int attemptCount = 0)
    {
        return new OutboxMessageEntity
        {
            OutboxMessageId = outboxMessageId,
            MessageId = Guid.NewGuid(),
            MessageType = "OrderCreated.v1",
            SchemaVersion = 1,
            AggregateId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            OccurredAtUtc = ClaimTimeUtc.AddMinutes(-1),
            Destination = "orders.events",
            Producer = "Order",
            Payload = "{}",
            CreatedAtUtc = ClaimTimeUtc.AddMinutes(-1),
            AttemptCount = attemptCount
        };
    }

    private sealed class SequenceClock(
        params DateTimeOffset[] values)
        : IClock
    {
        private readonly Queue<DateTimeOffset> _values =
            new(values);

        public DateTimeOffset UtcNow =>
            _values.Count > 0
                ? _values.Dequeue()
                : throw new InvalidOperationException(
                    "No clock value remains.");
    }

    private sealed class FakeOutboxTransport(
        Guid? failingMessageId = null)
        : IOutboxTransport
    {
        public List<Guid> PublishedMessageIds { get; } = [];

        public Task PublishAsync(
            OutboxMessageEntity message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message.OutboxMessageId == failingMessageId)
            {
                throw new InvalidOperationException(
                    "Simulated transport failure.");
            }

            PublishedMessageIds.Add(
                message.OutboxMessageId);

            return Task.CompletedTask;
        }
    }

    private sealed class CancellingOutboxTransport
        : IOutboxTransport
    {
        public Task PublishAsync(
            OutboxMessageEntity message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromCanceled(
                cancellationToken);
        }
    }

    private sealed class FakeOutboxMessageRepository(
        IReadOnlyList<OutboxMessageEntity> claimed)
        : IOutboxMessageRepository
    {
        public List<PublishedCall> Published { get; } = [];

        public List<FailedCall> Failed { get; } = [];

        public Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            int batchSize,
            Guid claimToken,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(claimed);
        }

        public Task MarkPublishedAsync(
            Guid outboxMessageId,
            Guid claimToken,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Published.Add(
                new PublishedCall(
                    outboxMessageId,
                    claimToken,
                    publishedAtUtc));

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid outboxMessageId,
            Guid claimToken,
            DateTimeOffset failedAtUtc,
            DateTimeOffset nextAttemptAtUtc,
            string errorCode,
            CancellationToken cancellationToken = default)
        {
            Failed.Add(
                new FailedCall(
                    outboxMessageId,
                    claimToken,
                    failedAtUtc,
                    nextAttemptAtUtc,
                    errorCode));

            return Task.CompletedTask;
        }
    }

    private sealed record PublishedCall(
        Guid OutboxMessageId,
        Guid ClaimToken,
        DateTimeOffset PublishedAtUtc);

    private sealed record FailedCall(
        Guid OutboxMessageId,
        Guid ClaimToken,
        DateTimeOffset FailedAtUtc,
        DateTimeOffset NextAttemptAtUtc,
        string ErrorCode);
}
