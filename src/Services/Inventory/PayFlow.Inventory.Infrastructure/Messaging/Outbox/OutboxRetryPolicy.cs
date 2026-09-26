namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed class OutboxRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public OutboxRetryPolicy(
        TimeSpan baseDelay,
        TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            baseDelay,
            TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxDelay,
            baseDelay);

        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    public TimeSpan GetDelay(int previousAttemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            previousAttemptCount);

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
