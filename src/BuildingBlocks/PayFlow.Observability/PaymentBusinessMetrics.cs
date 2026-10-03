using System.Diagnostics.Metrics;

namespace PayFlow.Observability;

public static class PaymentBusinessMetrics
{
    public const string MeterName = "PayFlow.Payment";

    private static readonly Meter Meter =
        new(MeterName);

    private static readonly Counter<long> ProviderOperationOutcomes =
        Meter.CreateCounter<long>(
            "payflow.payment.provider.operation",
            unit: "{attempt}",
            description: "Finalized payment provider attempts by operation and outcome.");

    private static readonly Counter<long> ReconciliationRequests =
        Meter.CreateCounter<long>(
            "payflow.payment.reconciliation.requested",
            unit: "{request}",
            description: "Unique payment reconciliation requests committed by the Payment consumer.");

    public static void RecordProviderOperation(
        string operation,
        string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        ProviderOperationOutcomes.Add(
            1,
            new KeyValuePair<string, object?>(
                "operation",
                operation),
            new KeyValuePair<string, object?>(
                "outcome",
                outcome));
    }

    public static void RecordReconciliationRequested()
    {
        ReconciliationRequests.Add(1);
    }
}
