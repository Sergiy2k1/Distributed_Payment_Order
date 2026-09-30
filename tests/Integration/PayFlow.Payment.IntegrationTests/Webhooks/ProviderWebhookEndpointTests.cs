using Microsoft.AspNetCore.Http;
using PayFlow.Payment.Api.Webhooks;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Webhooks;

public sealed class ProviderWebhookEndpointTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReceivedAtUtc =
        OccurredAtUtc.AddSeconds(1);

    [Fact]
    public async Task FirstDeliveryIsAcceptedAndDuplicateIsAcknowledged()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var request =
            CreateRequest(
                Guid.NewGuid(),
                providerReference: "provider-capture-001");

        var timeProvider =
            new FixedTimeProvider(
                ReceivedAtUtc);

        await using var firstDbContext =
            fixture.CreateDbContext();

        var first =
            await ProviderWebhookEndpoints.ReceiveAsync(
                request,
                new ProviderWebhookInboxRepository(
                    firstDbContext),
                timeProvider,
                cancellationToken);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            GetStatusCode(first));

        await using var duplicateDbContext =
            fixture.CreateDbContext();

        var duplicate =
            await ProviderWebhookEndpoints.ReceiveAsync(
                request,
                new ProviderWebhookInboxRepository(
                    duplicateDbContext),
                timeProvider,
                cancellationToken);

        Assert.Equal(
            StatusCodes.Status200OK,
            GetStatusCode(duplicate));
    }

    [Fact]
    public async Task SameEventIdWithDifferentLogicalPayloadReturnsConflict()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var eventId =
            Guid.NewGuid();

        var original =
            CreateRequest(
                eventId,
                providerReference: "provider-capture-001");

        var conflicting =
            original with
            {
                ProviderReference =
                    "provider-capture-CHANGED"
            };

        var timeProvider =
            new FixedTimeProvider(
                ReceivedAtUtc);

        await using (var firstDbContext =
            fixture.CreateDbContext())
        {
            var first =
                await ProviderWebhookEndpoints.ReceiveAsync(
                    original,
                    new ProviderWebhookInboxRepository(
                        firstDbContext),
                    timeProvider,
                    cancellationToken);

            Assert.Equal(
                StatusCodes.Status202Accepted,
                GetStatusCode(first));
        }

        await using var conflictDbContext =
            fixture.CreateDbContext();

        var conflict =
            await ProviderWebhookEndpoints.ReceiveAsync(
                conflicting,
                new ProviderWebhookInboxRepository(
                    conflictDbContext),
                timeProvider,
                cancellationToken);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            GetStatusCode(conflict));
    }

    [Fact]
    public async Task RefundWebhookRequiresRefundId()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var request =
            CreateRequest(
                Guid.NewGuid(),
                providerReference:
                    "provider-refund-001")
            with
            {
                OperationType = "Refund",
                RefundId = null
            };

        await using var dbContext =
            fixture.CreateDbContext();

        var actual =
            await ProviderWebhookEndpoints.ReceiveAsync(
                request,
                new ProviderWebhookInboxRepository(
                    dbContext),
                new FakeTimeProvider(
                    ReceivedAtUtc),
                cancellationToken);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            GetStatusCode(actual));
    }

    private static int? GetStatusCode(
        IResult result)
    {
        return Assert.IsAssignableFrom<
                IStatusCodeHttpResult>(result)
            .StatusCode;
    }

    private static ProviderWebhookRequest CreateRequest(
        Guid eventId,
        string providerReference)
    {
        return new ProviderWebhookRequest(
            eventId,
            "PaymentProviderResult.v1",
            "Capture",
            Guid.NewGuid(),
            null,
            "Succeeded",
            providerReference,
            null,
            OccurredAtUtc);
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            if (utcNow.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Timestamp must use UTC offset.",
                    nameof(utcNow));
            }

            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
