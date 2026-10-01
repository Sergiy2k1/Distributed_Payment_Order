using System.Diagnostics.Metrics;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Checkout;

public static class SagaBusinessMetrics
{
    public const string MeterName = "PayFlow.Saga";

    private static readonly Meter Meter =
        new(MeterName);

    private static readonly Counter<long> CheckoutTimeouts =
        Meter.CreateCounter<long>(
            "payflow.saga.checkout.timeout",
            unit: "{timeout}",
            description: "Checkout saga deadlines that elapsed.");

    private static readonly Counter<long> CheckoutTimeoutActions =
        Meter.CreateCounter<long>(
            "payflow.saga.checkout.timeout.action",
            unit: "{action}",
            description: "Actions selected after a checkout saga timeout.");

    private static readonly Counter<long> PostCaptureCompensations =
        Meter.CreateCounter<long>(
            "payflow.saga.checkout.timeout.post_capture_compensation",
            unit: "{compensation}",
            description: "Post-capture compensations started because of checkout timeouts.");

    private static readonly Counter<long> PaymentReconciliations =
        Meter.CreateCounter<long>(
            "payflow.saga.checkout.timeout.payment_reconciliation",
            unit: "{reconciliation}",
            description: "Payment reconciliations requested because of checkout timeouts.");

    public static void RecordTimeout(
        CheckoutSagaStatus status,
        CheckoutSagaTimeoutAction action)
    {
        var statusTag =
            new KeyValuePair<string, object?>(
                "saga.status",
                status.ToString());

        var actionTag =
            new KeyValuePair<string, object?>(
                "action",
                action.ToString());

        CheckoutTimeouts.Add(
            1,
            statusTag);

        CheckoutTimeoutActions.Add(
            1,
            statusTag,
            actionTag);

        if (action
            == CheckoutSagaTimeoutAction.PostCaptureCompensationStarted)
        {
            PostCaptureCompensations.Add(
                1,
                statusTag);
        }

        if (action
            == CheckoutSagaTimeoutAction.ReconciliationRequested)
        {
            PaymentReconciliations.Add(
                1,
                statusTag);
        }
    }
}
