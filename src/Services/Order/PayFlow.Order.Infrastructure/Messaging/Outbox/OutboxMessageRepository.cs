using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed class OutboxMessageRepository
{
    private readonly OrderDbContext _dbContext;

    public OutboxMessageRepository(
        OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OutboxMessageEntity>> GetPendingAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureBatchSize(batchSize);

        return await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.PublishedAtUtc == null
                && (message.NextAttemptAtUtc == null
                    || message.NextAttemptAtUtc <= nowUtc)
                && (message.ClaimToken == null
                    || message.ClaimedUntilUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.OutboxMessageId)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        Guid claimToken,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsureBatchSize(batchSize);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                leaseDuration,
                "Lease duration must be greater than zero.");
        }

        if (claimToken == Guid.Empty)
        {
            throw new ArgumentException(
                "Claim token cannot be empty.",
                nameof(claimToken));
        }

        var claimedUntilUtc = nowUtc.Add(leaseDuration);

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var messages = await _dbContext.OutboxMessages
            .FromSqlInterpolated(
                $"""
                 SELECT *
                 FROM outbox_messages
                 WHERE published_at_utc IS NULL
                   AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {nowUtc})
                   AND (claim_token IS NULL OR claimed_until_utc <= {nowUtc})
                 ORDER BY created_at_utc, outbox_message_id
                 FOR UPDATE SKIP LOCKED
                 LIMIT {batchSize}
                 """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            message.ClaimToken = claimToken;
            message.ClaimedUntilUtc = claimedUntilUtc;
        }

        await _dbContext
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction
            .CommitAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            _dbContext.Entry(message).State =
                EntityState.Detached;
        }

        return messages;
    }

    public async Task MarkPublishedAsync(
        Guid outboxMessageId,
        Guid claimToken,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(
            outboxMessageId,
            nameof(outboxMessageId));
        EnsureIdentity(
            claimToken,
            nameof(claimToken));
        EnsureUtc(
            publishedAtUtc,
            nameof(publishedAtUtc));

        var affectedRows = await _dbContext.OutboxMessages
            .Where(message =>
                message.OutboxMessageId == outboxMessageId
                && message.PublishedAtUtc == null
                && message.ClaimToken == claimToken
                && message.ClaimedUntilUtc >= publishedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        message => message.AttemptCount,
                        message => message.AttemptCount + 1)
                    .SetProperty(
                        message => message.PublishedAtUtc,
                        publishedAtUtc)
                    .SetProperty(
                        message => message.NextAttemptAtUtc,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        message => message.LastErrorCode,
                        (string?)null)
                    .SetProperty(
                        message => message.ClaimToken,
                        (Guid?)null)
                    .SetProperty(
                        message => message.ClaimedUntilUtc,
                        (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);

        EnsureLeaseOwned(affectedRows);
    }

    public async Task MarkFailedAsync(
        Guid outboxMessageId,
        Guid claimToken,
        DateTimeOffset failedAtUtc,
        DateTimeOffset nextAttemptAtUtc,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(
            outboxMessageId,
            nameof(outboxMessageId));
        EnsureIdentity(
            claimToken,
            nameof(claimToken));
        EnsureUtc(
            failedAtUtc,
            nameof(failedAtUtc));
        EnsureUtc(
            nextAttemptAtUtc,
            nameof(nextAttemptAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            errorCode);

        if (nextAttemptAtUtc <= failedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAtUtc),
                nextAttemptAtUtc,
                "Next attempt time must be after the failed attempt.");
        }

        if (errorCode.Length > 128)
        {
            throw new ArgumentException(
                "Error code cannot exceed 128 characters.",
                nameof(errorCode));
        }

        var affectedRows = await _dbContext.OutboxMessages
            .Where(message =>
                message.OutboxMessageId == outboxMessageId
                && message.PublishedAtUtc == null
                && message.ClaimToken == claimToken
                && message.ClaimedUntilUtc >= failedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        message => message.AttemptCount,
                        message => message.AttemptCount + 1)
                    .SetProperty(
                        message => message.NextAttemptAtUtc,
                        nextAttemptAtUtc)
                    .SetProperty(
                        message => message.LastErrorCode,
                        errorCode)
                    .SetProperty(
                        message => message.ClaimToken,
                        (Guid?)null)
                    .SetProperty(
                        message => message.ClaimedUntilUtc,
                        (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);

        EnsureLeaseOwned(affectedRows);
    }

    private static void EnsureBatchSize(
        int batchSize)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }
    }

    private static void EnsureIdentity(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty.",
                parameterName);
        }
    }

    private static void EnsureLeaseOwned(
        int affectedRows)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                "The Outbox claim is missing, expired, or owned by another publisher.");
        }
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
