using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence.Entities;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Webhooks;

public sealed class ProviderWebhookPollingRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset BaseTimeUtc =
        new(2026, 9, 30, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReturnsOldestUnprocessedWebhook()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var olderEventId =
            Guid.NewGuid();
        var newerEventId =
            Guid.NewGuid();

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new ProviderWebhookInboxRepository(
                    dbContext);

            Assert.Equal(
                ProviderWebhookInsertResult.Inserted,
                await repository.TryInsertAsync(
                    CreateWebhook(
                        newerEventId,
                        BaseTimeUtc.AddSeconds(2),
                        Hash('A')),
                    cancellationToken));

            Assert.Equal(
                ProviderWebhookInsertResult.Inserted,
                await repository.TryInsertAsync(
                    CreateWebhook(
                        olderEventId,
                        BaseTimeUtc.AddSeconds(1),
                        Hash('B')),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var verificationRepository =
            new ProviderWebhookInboxRepository(
                verificationDbContext);

        var actual =
            await verificationRepository
                .GetNextUnprocessedEventIdAsync(
                    cancellationToken);

        Assert.Equal(
            olderEventId,
            actual);

        await verificationRepository.MarkProcessedAsync(
            olderEventId,
            BaseTimeUtc.AddSeconds(3),
            cancellationToken);

        await verificationRepository.MarkProcessedAsync(
            newerEventId,
            BaseTimeUtc.AddSeconds(4),
            cancellationToken);
    }

    [Fact]
    public async Task ProcessedWebhookIsSkippedByPollingQuery()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var processedEventId =
            Guid.NewGuid();
        var pendingEventId =
            Guid.NewGuid();

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new ProviderWebhookInboxRepository(
                    dbContext);

            await repository.TryInsertAsync(
                CreateWebhook(
                    processedEventId,
                    BaseTimeUtc.AddSeconds(1),
                    Hash('C')),
                cancellationToken);

            await repository.TryInsertAsync(
                CreateWebhook(
                    pendingEventId,
                    BaseTimeUtc.AddSeconds(2),
                    Hash('D')),
                cancellationToken);

            await repository.MarkProcessedAsync(
                processedEventId,
                BaseTimeUtc.AddSeconds(3),
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var actual =
            await new ProviderWebhookInboxRepository(
                    verificationDbContext)
                .GetNextUnprocessedEventIdAsync(
                    cancellationToken);

        Assert.Equal(
            pendingEventId,
            actual);
    }

    private static ProviderWebhookInboxEntity CreateWebhook(
        Guid eventId,
        DateTimeOffset receivedAtUtc,
        string payloadHash)
    {
        return new ProviderWebhookInboxEntity
        {
            EventId = eventId,
            EventType = "PaymentProviderResult.v1",
            OperationType = "Capture",
            PaymentId = Guid.NewGuid(),
            RefundId = null,
            Outcome = "Succeeded",
            ProviderReference = "provider-reference",
            ErrorCode = null,
            OccurredAtUtc = BaseTimeUtc,
            ReceivedAtUtc = receivedAtUtc,
            ProcessedAtUtc = null,
            PayloadHash = payloadHash
        };
    }

    private static string Hash(
        char value)
    {
        return new string(
            value,
            64);
    }
}
