using PayFlow.Order.Application.Abstractions;

namespace PayFlow.Order.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
