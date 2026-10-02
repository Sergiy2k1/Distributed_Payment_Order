using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Infrastructure.Messaging.Outbox;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;
using PayFlow.Inventory.IntegrationTests.Infrastructure;

namespace PayFlow.Inventory.IntegrationTests.Messaging.Outbox;

public sealed class OutboxBacklogSnapshotTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 10, 2, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SnapshotCountsEveryUnpublishedMessageAndUsesOldestCreationTime()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using (var setupDbContext =
            fixture.CreateDbContext())
        {
            await setupDbContext.OutboxMessages
                .ExecuteDeleteAsync(cancellationToken);

            setupDbContext.OutboxMessages.AddRange(
                CreateMessage(
                    NowUtc.AddMinutes(-7),
                    nextAttemptAtUtc: NowUtc.AddMinutes(5)),
                CreateMessage(
                    NowUtc.AddMinutes(-3),
                    claimToken: Guid.NewGuid(),
                    claimedUntilUtc: NowUtc.AddMinutes(1)),
                CreateMessage(
                    NowUtc.AddMinutes(-12),
                    publishedAtUtc: NowUtc.AddMinutes(-11)));

            await setupDbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new OutboxMessageRepository(dbContext);

        var snapshot = await repository
            .GetBacklogSnapshotAsync(
                NowUtc,
                cancellationToken);

        Assert.Equal(2, snapshot.PendingMessages);
        Assert.Equal(420, snapshot.OldestPendingAgeSeconds);
    }

    private static OutboxMessageEntity CreateMessage(
        DateTimeOffset createdAtUtc,
        DateTimeOffset? nextAttemptAtUtc = null,
        DateTimeOffset? publishedAtUtc = null,
        Guid? claimToken = null,
        DateTimeOffset? claimedUntilUtc = null)
    {
        return new OutboxMessageEntity
        {
            OutboxMessageId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            MessageType = "InventoryReserved.v1",
            SchemaVersion = 1,
            AggregateId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
            OccurredAtUtc = createdAtUtc,
            Destination = "inventory.events",
            Producer = "Inventory",
            TraceParent = null,
            Payload = "{}",
            CreatedAtUtc = createdAtUtc,
            PublishedAtUtc = publishedAtUtc,
            AttemptCount = 0,
            NextAttemptAtUtc = nextAttemptAtUtc,
            LastErrorCode = null,
            ClaimToken = claimToken,
            ClaimedUntilUtc = claimedUntilUtc
        };
    }
}
