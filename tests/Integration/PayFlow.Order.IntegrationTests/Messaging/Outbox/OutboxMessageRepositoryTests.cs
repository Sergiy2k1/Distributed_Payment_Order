using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence.Entities;
using PayFlow.Order.IntegrationTests.Infrastructure;

namespace PayFlow.Order.IntegrationTests.Messaging.Outbox;

public sealed class OutboxMessageRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 9, 24, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPendingReturnsOnlyEligibleMessagesInStableOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var firstId =
            Guid.Parse("00000000-0000-0000-0000-000000000101");
        var secondId =
            Guid.Parse("00000000-0000-0000-0000-000000000102");

        await using (var setupDbContext =
            fixture.CreateDbContext())
        {
            setupDbContext.OutboxMessages.AddRange(
                CreateMessage(
                    firstId,
                    NowUtc.AddMinutes(-4),
                    nextAttemptAtUtc: null),
                CreateMessage(
                    secondId,
                    NowUtc.AddMinutes(-3),
                    nextAttemptAtUtc: NowUtc.AddMinutes(-1)),
                CreateMessage(
                    Guid.NewGuid(),
                    NowUtc.AddMinutes(-2),
                    nextAttemptAtUtc: NowUtc.AddMinutes(5)),
                CreateMessage(
                    Guid.NewGuid(),
                    NowUtc.AddMinutes(-1),
                    publishedAtUtc: NowUtc.AddSeconds(-30)));

            await setupDbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new OutboxMessageRepository(dbContext);

        var actual = await repository.GetPendingAsync(
            NowUtc,
            batchSize: 10,
            cancellationToken);

        var selected = actual
            .Where(message =>
                message.OutboxMessageId == firstId
                || message.OutboxMessageId == secondId)
            .ToArray();

        Assert.Equal(2, selected.Length);
        Assert.Equal(firstId, selected[0].OutboxMessageId);
        Assert.Equal(secondId, selected[1].OutboxMessageId);
    }

    [Fact]
    public async Task MarkPublishedPersistsSuccessfulAttemptState()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var publishedAtUtc =
            NowUtc.AddSeconds(5);

        await SeedAsync(
            CreateMessage(
                outboxMessageId,
                NowUtc.AddMinutes(-1),
                nextAttemptAtUtc: NowUtc.AddSeconds(-1)),
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new OutboxMessageRepository(dbContext);

            var message = Assert.Single(
                await repository.GetPendingAsync(
                    NowUtc,
                    batchSize: 100,
                    cancellationToken),
                candidate =>
                    candidate.OutboxMessageId
                        == outboxMessageId);

            OutboxMessageRepository.MarkPublished(
                message,
                publishedAtUtc);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var persisted =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.OutboxMessageId
                            == outboxMessageId,
                    cancellationToken);

        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(
            publishedAtUtc,
            persisted.PublishedAtUtc);
        Assert.Null(persisted.NextAttemptAtUtc);
        Assert.Null(persisted.LastErrorCode);
    }

    [Fact]
    public async Task MarkFailedPersistsRetryMetadata()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var nextAttemptAtUtc =
            NowUtc.AddSeconds(30);

        await SeedAsync(
            CreateMessage(
                outboxMessageId,
                NowUtc.AddMinutes(-1)),
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new OutboxMessageRepository(dbContext);

            var message = Assert.Single(
                await repository.GetPendingAsync(
                    NowUtc,
                    batchSize: 100,
                    cancellationToken),
                candidate =>
                    candidate.OutboxMessageId
                        == outboxMessageId);

            OutboxMessageRepository.MarkFailed(
                message,
                nextAttemptAtUtc,
                "broker_unavailable");

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var persisted =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.OutboxMessageId
                            == outboxMessageId,
                    cancellationToken);

        Assert.Equal(1, persisted.AttemptCount);
        Assert.Null(persisted.PublishedAtUtc);
        Assert.Equal(
            nextAttemptAtUtc,
            persisted.NextAttemptAtUtc);
        Assert.Equal(
            "broker_unavailable",
            persisted.LastErrorCode);
    }

    private async Task SeedAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        await dbContext.OutboxMessages.AddAsync(
            message,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static OutboxMessageEntity CreateMessage(
        Guid outboxMessageId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? nextAttemptAtUtc = null,
        DateTimeOffset? publishedAtUtc = null)
    {
        return new OutboxMessageEntity
        {
            OutboxMessageId = outboxMessageId,
            MessageId = Guid.NewGuid(),
            MessageType = "OrderCreated.v1",
            SchemaVersion = 1,
            AggregateId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
            OccurredAtUtc = createdAtUtc,
            Destination = "orders.events",
            Producer = "Order",
            TraceParent = null,
            Payload = "{}",
            CreatedAtUtc = createdAtUtc,
            PublishedAtUtc = publishedAtUtc,
            AttemptCount = 0,
            NextAttemptAtUtc = nextAttemptAtUtc,
            LastErrorCode = null
        };
    }
}
