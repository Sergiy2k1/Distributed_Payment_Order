using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Messaging.Webhooks;

public sealed class ProviderWebhookInboxRepository
{
    private readonly PaymentDbContext _dbContext;

    public ProviderWebhookInboxRepository(
        PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProviderWebhookInsertResult> TryInsertAsync(
        ProviderWebhookInboxEntity webhook,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        Validate(webhook);

        var affectedRows =
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO provider_webhook_inbox
                 (
                     event_id,
                     event_type,
                     operation_type,
                     payment_id,
                     refund_id,
                     outcome,
                     provider_reference,
                     error_code,
                     occurred_at_utc,
                     received_at_utc,
                     processed_at_utc,
                     payload_hash
                 )
                 VALUES
                 (
                     {webhook.EventId},
                     {webhook.EventType},
                     {webhook.OperationType},
                     {webhook.PaymentId},
                     {webhook.RefundId},
                     {webhook.Outcome},
                     {webhook.ProviderReference},
                     {webhook.ErrorCode},
                     {webhook.OccurredAtUtc},
                     {webhook.ReceivedAtUtc},
                     NULL,
                     {webhook.PayloadHash}
                 )
                 ON CONFLICT (event_id)
                 DO NOTHING
                 """,
                cancellationToken)
            .ConfigureAwait(false);

        if (affectedRows == 1)
        {
            return ProviderWebhookInsertResult.Inserted;
        }

        var existingHash =
            await _dbContext.ProviderWebhookInbox
                .AsNoTracking()
                .Where(
                    existing =>
                        existing.EventId
                        == webhook.EventId)
                .Select(
                    existing =>
                        existing.PayloadHash)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);

        return string.Equals(
                existingHash,
                webhook.PayloadHash,
                StringComparison.Ordinal)
            ? ProviderWebhookInsertResult.Duplicate
            : ProviderWebhookInsertResult.Conflict;
    }

    public Task<Guid?> GetNextUnprocessedEventIdAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProviderWebhookInbox
            .AsNoTracking()
            .Where(
                webhook =>
                    webhook.ProcessedAtUtc == null)
            .OrderBy(
                webhook =>
                    webhook.ReceivedAtUtc)
            .ThenBy(
                webhook =>
                    webhook.EventId)
            .Select(
                webhook =>
                    (Guid?)webhook.EventId)
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public Task<ProviderWebhookInboxEntity?> GetByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId cannot be empty.",
                nameof(eventId));
        }

        return _dbContext.ProviderWebhookInbox
            .AsNoTracking()
            .SingleOrDefaultAsync(
                webhook =>
                    webhook.EventId == eventId,
                cancellationToken);
    }

    public async Task MarkProcessedAsync(
        Guid eventId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId cannot be empty.",
                nameof(eventId));
        }

        EnsureUtc(
            processedAtUtc,
            nameof(processedAtUtc));

        var affectedRows =
            await _dbContext.ProviderWebhookInbox
                .Where(
                    webhook =>
                        webhook.EventId == eventId
                        && webhook.ProcessedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            webhook => webhook.ProcessedAtUtc,
                            processedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                "Webhook event is missing or already processed.");
        }
    }

    private static void Validate(
        ProviderWebhookInboxEntity webhook)
    {
        if (webhook.EventId == Guid.Empty
            || webhook.PaymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId and PaymentId must be non-empty.",
                nameof(webhook));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            webhook.EventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            webhook.OperationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            webhook.Outcome);

        if (webhook.PayloadHash.Length != 64)
        {
            throw new ArgumentException(
                "PayloadHash must contain a 64-character SHA-256 hex value.",
                nameof(webhook));
        }

        EnsureUtc(
            webhook.OccurredAtUtc,
            nameof(webhook.OccurredAtUtc));
        EnsureUtc(
            webhook.ReceivedAtUtc,
            nameof(webhook.ReceivedAtUtc));
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
