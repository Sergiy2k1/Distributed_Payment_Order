using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Infrastructure.Persistence.Entities;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Persistence;

public sealed class InboxMessageRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 25, 20, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAtUtc =
        OccurredAtUtc.AddSeconds(1);

    private static readonly DateTimeOffset ProcessedAtUtc =
        OccurredAtUtc.AddSeconds(2);

    [Fact]
    public async Task TryInsertPersistsFirstDelivery()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var message = CreateMessage(
            Guid.NewGuid(),
            "payflow.saga.checkout.v1",
            sourceOffset: 10);

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new InboxMessageRepository(dbContext);

        var inserted = await repository.TryInsertAsync(
            message,
            cancellationToken);

        Assert.True(inserted);

        var persisted =
            await dbContext.InboxMessages
                .AsNoTracking()
                .SingleAsync(
                    candidate =>
                        candidate.ConsumerName
                            == message.ConsumerName
                        && candidate.MessageId
                            == message.MessageId,
                    cancellationToken);

        Assert.Equal(
            message.SourceTopic,
            persisted.SourceTopic);
        Assert.Equal(
            message.SourcePartition,
            persisted.SourcePartition);
        Assert.Equal(
            message.SourceOffset,
            persisted.SourceOffset);
    }

    [Fact]
    public async Task TryInsertTreatsDuplicateDeliveryAsNoOp()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var first = CreateMessage(
            messageId,
            "payflow.saga.checkout.v1",
            sourceOffset: 20);
        var duplicate = CreateMessage(
            messageId,
            "payflow.saga.checkout.v1",
            sourceOffset: 999);

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new InboxMessageRepository(dbContext);

        var firstInserted =
            await repository.TryInsertAsync(
                first,
                cancellationToken);

        var duplicateInserted =
            await repository.TryInsertAsync(
                duplicate,
                cancellationToken);

        Assert.True(firstInserted);
        Assert.False(duplicateInserted);

        var persisted =
            await dbContext.InboxMessages
                .AsNoTracking()
                .SingleAsync(
                    candidate =>
                        candidate.ConsumerName
                            == first.ConsumerName
                        && candidate.MessageId
                            == messageId,
                    cancellationToken);

        Assert.Equal(
            first.SourceOffset,
            persisted.SourceOffset);
    }

    [Fact]
    public async Task SameMessageIdCanBeProcessedByDifferentConsumers()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var firstConsumer = CreateMessage(
            messageId,
            "payflow.saga.checkout.v1",
            sourceOffset: 30);
        var secondConsumer = CreateMessage(
            messageId,
            "payflow.saga.audit.v1",
            sourceOffset: 30);

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new InboxMessageRepository(dbContext);

        Assert.True(
            await repository.TryInsertAsync(
                firstConsumer,
                cancellationToken));

        Assert.True(
            await repository.TryInsertAsync(
                secondConsumer,
                cancellationToken));

        var count = await dbContext.InboxMessages
            .AsNoTracking()
            .CountAsync(
                candidate =>
                    candidate.MessageId == messageId,
                cancellationToken);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ConcurrentDuplicateDeliveryHasSingleWinner()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var firstMessage = CreateMessage(
            messageId,
            "payflow.saga.checkout.v1",
            sourceOffset: 40);
        var secondMessage = CreateMessage(
            messageId,
            "payflow.saga.checkout.v1",
            sourceOffset: 41);

        await using var firstDbContext =
            fixture.CreateDbContext();
        await using var secondDbContext =
            fixture.CreateDbContext();

        var firstRepository =
            new InboxMessageRepository(
                firstDbContext);
        var secondRepository =
            new InboxMessageRepository(
                secondDbContext);

        var results = await Task.WhenAll(
            firstRepository.TryInsertAsync(
                firstMessage,
                cancellationToken),
            secondRepository.TryInsertAsync(
                secondMessage,
                cancellationToken));

        Assert.Single(
            results,
            inserted => inserted);
        Assert.Single(
            results,
            inserted => !inserted);

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var count =
            await verificationDbContext.InboxMessages
                .AsNoTracking()
                .CountAsync(
                    candidate =>
                        candidate.ConsumerName
                            == "payflow.saga.checkout.v1"
                        && candidate.MessageId
                            == messageId,
                    cancellationToken);

        Assert.Equal(1, count);
    }

    private static InboxMessageEntity CreateMessage(
        Guid messageId,
        string consumerName,
        long sourceOffset)
    {
        return new InboxMessageEntity
        {
            ConsumerName = consumerName,
            MessageId = messageId,
            MessageType = "OrderCreated.v1",
            SchemaVersion = 1,
            AggregateId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
            OccurredAtUtc = OccurredAtUtc,
            SourceTopic = "orders.events",
            SourcePartition = 0,
            SourceOffset = sourceOffset,
            ReceivedAtUtc = ReceivedAtUtc,
            ProcessedAtUtc = ProcessedAtUtc
        };
    }
}
