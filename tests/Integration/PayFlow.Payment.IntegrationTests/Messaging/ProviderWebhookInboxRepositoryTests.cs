using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence.Entities;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Messaging;

public sealed class ProviderWebhookInboxRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 30, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SameEventAndPayloadIsIdempotentDuplicate()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var webhook =
            CreateWebhook(
                Guid.NewGuid(),
                Hash('A'));

        await using var firstDbContext =
            fixture.CreateDbContext();

        var first =
            await new ProviderWebhookInboxRepository(
                    firstDbContext)
                .TryInsertAsync(
                    webhook,
                    cancellationToken);

        await using var duplicateDbContext =
            fixture.CreateDbContext();

        var duplicate =
            await new ProviderWebhookInboxRepository(
                    duplicateDbContext)
                .TryInsertAsync(
                    CreateWebhook(
                        webhook.EventId,
                        webhook.PayloadHash),
                    cancellationToken);

        Assert.Equal(
            ProviderWebhookInsertResult.Inserted,
            first);
        Assert.Equal(
            ProviderWebhookInsertResult.Duplicate,
            duplicate);
    }

    [Fact]
    public async Task SameEventWithDifferentPayloadIsConflict()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var eventId =
            Guid.NewGuid();

        await using (var firstDbContext =
            fixture.CreateDbContext())
        {
            Assert.Equal(
                ProviderWebhookInsertResult.Inserted,
                await new ProviderWebhookInboxRepository(
                        firstDbContext)
                    .TryInsertAsync(
                        CreateWebhook(
                            eventId,
                            Hash('B')),
                        cancellationToken));
        }

        await using var conflictDbContext =
            fixture.CreateDbContext();

        var conflict =
            await new ProviderWebhookInboxRepository(
                    conflictDbContext)
                .TryInsertAsync(
                    CreateWebhook(
                        eventId,
                        Hash('C')),
                    cancellationToken);

        Assert.Equal(
            ProviderWebhookInsertResult.Conflict,
            conflict);
    }

    [Fact]
    public async Task MarkProcessedPersistsCompletionTimestamp()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var eventId =
            Guid.NewGuid();
        var processedAtUtc =
            OccurredAtUtc.AddSeconds(5);

        await using (var insertDbContext =
            fixture.CreateDbContext())
        {
            await new ProviderWebhookInboxRepository(
                    insertDbContext)
                .TryInsertAsync(
                    CreateWebhook(
                        eventId,
                        Hash('D')),
                    cancellationToken);
        }

        await using (var processDbContext =
            fixture.CreateDbContext())
        {
            await new ProviderWebhookInboxRepository(
                    processDbContext)
                .MarkProcessedAsync(
                    eventId,
                    processedAtUtc,
                    cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var actual =
            await verificationDbContext
                .ProviderWebhookInbox
                .AsNoTracking()
                .SingleAsync(
                    webhook =>
                        webhook.EventId == eventId,
                    cancellationToken);

        Assert.Equal(
            processedAtUtc,
            actual.ProcessedAtUtc);
    }

    private static ProviderWebhookInboxEntity CreateWebhook(
        Guid eventId,
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
            OccurredAtUtc = OccurredAtUtc,
            ReceivedAtUtc = OccurredAtUtc.AddSeconds(1),
            ProcessedAtUtc = null,
            PayloadHash = payloadHash
        };
    }

    private static string Hash(char value)
    {
        return new string(
            value,
            64);
    }
}
