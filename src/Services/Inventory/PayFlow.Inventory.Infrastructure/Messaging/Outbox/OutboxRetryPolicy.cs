namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

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
                nameof(baseDelay));
        }

        if (maxDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDelay));
        }

        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    public TimeSpan GetDelay(int previousAttemptCount)
    {
        if (previousAttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousAttemptCount));
        }

        var ticks = _baseDelay.Ticks;

        for (var attempt = 0;
             attempt < previousAttemptCount
             && ticks < _maxDelay.Ticks;
             attempt++)
        {
            ticks = Math.Min(
                checked(ticks * 2),
                _maxDelay.Ticks);
        }

        return TimeSpan.FromTicks(ticks);
    }
}
