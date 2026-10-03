using System.Diagnostics.Metrics;

namespace PayFlow.Observability;

public static class InboxMetrics
{
    public const string MeterName = "PayFlow.Inbox";

    private static readonly Meter Meter =
        new(MeterName);

    private static readonly Counter<long> ProcessingOutcomes =
        Meter.CreateCounter<long>(
            "payflow.inbox.processing",
            unit: "{message}",
            description: "Committed Inbox processing attempts by message type and outcome.");

    public static void Record(
        string messageType,
        bool processed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        ProcessingOutcomes.Add(
            1,
            new KeyValuePair<string, object?>(
                "message.type",
                messageType),
            new KeyValuePair<string, object?>(
                "outcome",
                processed
                    ? "processed"
                    : "duplicate"));
    }
}
