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

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        return await _dbContext.OutboxMessages
            .Where(message =>
                message.PublishedAtUtc == null
                && (message.NextAttemptAtUtc == null
                    || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.OutboxMessageId)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static void MarkPublished(
        OutboxMessageEntity message,
        DateTimeOffset publishedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureUtc(
            publishedAtUtc,
            nameof(publishedAtUtc));

        if (message.PublishedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "Outbox message has already been published.");
        }

        message.AttemptCount++;
        message.PublishedAtUtc = publishedAtUtc;
        message.NextAttemptAtUtc = null;
        message.LastErrorCode = null;
    }

    public static void MarkFailed(
        OutboxMessageEntity message,
        DateTimeOffset nextAttemptAtUtc,
        string errorCode)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureUtc(
            nextAttemptAtUtc,
            nameof(nextAttemptAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            errorCode);

        if (message.PublishedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "Published Outbox messages cannot be retried.");
        }

        if (errorCode.Length > 128)
        {
            throw new ArgumentException(
                "Error code cannot exceed 128 characters.",
                nameof(errorCode));
        }

        message.AttemptCount++;
        message.NextAttemptAtUtc = nextAttemptAtUtc;
        message.LastErrorCode = errorCode;
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
