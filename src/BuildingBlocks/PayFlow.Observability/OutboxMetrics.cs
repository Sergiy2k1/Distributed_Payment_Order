using System.Diagnostics.Metrics;

namespace PayFlow.Observability;

public static class OutboxMetrics
{
    public const string MeterName = "PayFlow.Outbox";

    private static readonly Meter Meter =
        new(MeterName);

    private static long _pendingMessages;
    private static long _oldestPendingAgeSeconds;

    static OutboxMetrics()
    {
        _ = Meter.CreateObservableGauge<long>(
            "payflow.outbox.pending_messages",
            () => Volatile.Read(ref _pendingMessages),
            unit: "{message}",
            description: "Unpublished Outbox messages currently waiting for delivery.");

        _ = Meter.CreateObservableGauge<long>(
            "payflow.outbox.oldest_pending_age",
            () => Volatile.Read(ref _oldestPendingAgeSeconds),
            unit: "s",
            description: "Age in seconds of the oldest unpublished Outbox message.");
    }

    public static void Observe(
        long pendingMessages,
        long oldestPendingAgeSeconds)
    {
        if (pendingMessages < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pendingMessages),
                pendingMessages,
                "Pending message count cannot be negative.");
        }

        if (oldestPendingAgeSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(oldestPendingAgeSeconds),
                oldestPendingAgeSeconds,
                "Oldest pending message age cannot be negative.");
        }

        if (pendingMessages == 0
            && oldestPendingAgeSeconds != 0)
        {
            throw new ArgumentException(
                "Oldest pending message age must be zero when the backlog is empty.",
                nameof(oldestPendingAgeSeconds));
        }

        Volatile.Write(
            ref _pendingMessages,
            pendingMessages);
        Volatile.Write(
            ref _oldestPendingAgeSeconds,
            oldestPendingAgeSeconds);
    }
}
