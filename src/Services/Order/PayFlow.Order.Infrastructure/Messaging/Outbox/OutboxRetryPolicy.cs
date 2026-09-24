namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed class OutboxRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public OutboxRetryPolicy(
        TimeSpan baseDelay,
        TimeSpan maxDelay)
    {
        if (baseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseDelay),
                baseDelay,
                "Base retry delay must be greater than zero.");
        }

        if (maxDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDelay),
                maxDelay,
                "Maximum retry delay must be greater than or equal to the base retry delay.");
        }

        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    public TimeSpan GetDelay(
        int previousAttemptCount)
    {
        if (previousAttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousAttemptCount),
                previousAttemptCount,
                "Attempt count cannot be negative.");
        }

        var delayTicks = _baseDelay.Ticks;
        var maxTicks = _maxDelay.Ticks;

        for (var attempt = 0;
             attempt < previousAttemptCount
             && delayTicks < maxTicks;
             attempt++)
        {
            delayTicks = delayTicks > maxTicks / 2
                ? maxTicks
                : Math.Min(delayTicks * 2, maxTicks);
        }

        return TimeSpan.FromTicks(delayTicks);
    }
}
