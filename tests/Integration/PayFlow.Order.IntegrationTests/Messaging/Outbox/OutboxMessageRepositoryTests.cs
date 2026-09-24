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
                    NowUtc.AddMinutes(-4)),
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
                    publishedAtUtc: NowUtc.AddSeconds(-30)),
                CreateMessage(
                    Guid.NewGuid(),
                    NowUtc.AddMinutes(-5),
                    claimToken: Guid.NewGuid(),
                    claimedUntilUtc: NowUtc.AddMinutes(1)));

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
    public async Task ClaimPendingSkipsRowLockedByAnotherPublisher()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var lockedId =
            Guid.Parse("00000000-0000-0000-0000-000000000201");
        var availableId =
            Guid.Parse("00000000-0000-0000-0000-000000000202");
        var claimToken = Guid.NewGuid();

        await SeedAsync(
            CreateMessage(
                lockedId,
                NowUtc.AddMinutes(-2)),
            cancellationToken);
        await SeedAsync(
            CreateMessage(
                availableId,
                NowUtc.AddMinutes(-1)),
            cancellationToken);

        await using var lockingDbContext =
            fixture.CreateDbContext();
        await using var transaction =
            await lockingDbContext.Database
                .BeginTransactionAsync(cancellationToken);

        _ = await lockingDbContext.OutboxMessages
            .FromSqlInterpolated(
                $"""
                 SELECT *
                 FROM outbox_messages
                 WHERE outbox_message_id = {lockedId}
                 FOR UPDATE
                 """)
            .SingleAsync(cancellationToken);

        await using var claimingDbContext =
            fixture.CreateDbContext();
        var repository =
            new OutboxMessageRepository(
                claimingDbContext);

        var claimed = await repository.ClaimPendingAsync(
            NowUtc,
            TimeSpan.FromSeconds(30),
            batchSize: 1,
            claimToken,
            cancellationToken);

        var message = Assert.Single(claimed);

        Assert.Equal(
            availableId,
            message.OutboxMessageId);
        Assert.Equal(
            claimToken,
            message.ClaimToken);
        Assert.Equal(
            NowUtc.AddSeconds(30),
            message.ClaimedUntilUtc);

        await transaction.RollbackAsync(
            cancellationToken);
    }

    [Fact]
    public async Task ClaimPendingDoesNotReturnActivelyLeasedMessage()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var firstClaimToken = Guid.NewGuid();

        await SeedAsync(
            CreateMessage(
                outboxMessageId,
                NowUtc.AddMinutes(-1)),
            cancellationToken);

        await using (var firstDbContext =
            fixture.CreateDbContext())
        {
            var firstRepository =
                new OutboxMessageRepository(
                    firstDbContext);

            var firstClaim =
                await firstRepository.ClaimPendingAsync(
                    NowUtc,
                    TimeSpan.FromMinutes(1),
                    batchSize: 100,
                    firstClaimToken,
                    cancellationToken);

            Assert.Single(
                firstClaim,
                message =>
                    message.OutboxMessageId
                        == outboxMessageId);
        }

        await using var secondDbContext =
            fixture.CreateDbContext();
        var secondRepository =
            new OutboxMessageRepository(
                secondDbContext);

        var secondClaim =
            await secondRepository.ClaimPendingAsync(
                NowUtc.AddSeconds(30),
                TimeSpan.FromMinutes(1),
                batchSize: 100,
                Guid.NewGuid(),
                cancellationToken);

        Assert.DoesNotContain(
            secondClaim,
            message =>
                message.OutboxMessageId
                    == outboxMessageId);
    }

    [Fact]
    public async Task MarkPublishedPersistsSuccessfulAttemptState()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        var publishedAtUtc =
            NowUtc.AddSeconds(5);

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

            _ = Assert.Single(
                await repository.ClaimPendingAsync(
                    NowUtc,
                    TimeSpan.FromMinutes(1),
                    batchSize: 100,
                    claimToken,
                    cancellationToken),
                candidate =>
                    candidate.OutboxMessageId
                        == outboxMessageId);

            await repository.MarkPublishedAsync(
                outboxMessageId,
                claimToken,
                publishedAtUtc,
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
        Assert.Null(persisted.ClaimToken);
        Assert.Null(persisted.ClaimedUntilUtc);
    }

    [Fact]
    public async Task MarkFailedPersistsRetryMetadataAndReleasesLease()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        var failedAtUtc =
            NowUtc.AddSeconds(5);
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

            _ = Assert.Single(
                await repository.ClaimPendingAsync(
                    NowUtc,
                    TimeSpan.FromMinutes(1),
                    batchSize: 100,
                    claimToken,
                    cancellationToken),
                candidate =>
                    candidate.OutboxMessageId
                        == outboxMessageId);

            await repository.MarkFailedAsync(
                outboxMessageId,
                claimToken,
                failedAtUtc,
                nextAttemptAtUtc,
                "broker_unavailable",
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
        Assert.Null(persisted.ClaimToken);
        Assert.Null(persisted.ClaimedUntilUtc);
    }

    [Fact]
    public async Task MarkPublishedRejectsStaleClaim()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var outboxMessageId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();

        await SeedAsync(
            CreateMessage(
                outboxMessageId,
                NowUtc.AddMinutes(-1)),
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();
        var repository =
            new OutboxMessageRepository(dbContext);

        _ = await repository.ClaimPendingAsync(
            NowUtc,
            TimeSpan.FromSeconds(10),
            batchSize: 100,
            claimToken,
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.MarkPublishedAsync(
                outboxMessageId,
                claimToken,
                NowUtc.AddSeconds(11),
                cancellationToken));
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
        DateTimeOffset? publishedAtUtc = null,
        Guid? claimToken = null,
        DateTimeOffset? claimedUntilUtc = null)
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
            LastErrorCode = null,
            ClaimToken = claimToken,
            ClaimedUntilUtc = claimedUntilUtc
        };
    }
}
