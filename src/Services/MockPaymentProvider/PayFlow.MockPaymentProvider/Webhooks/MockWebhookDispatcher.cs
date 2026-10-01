using PayFlow.MockPaymentProvider.Capture;
using PayFlow.MockPaymentProvider.Refund;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed class MockWebhookDispatcher
{
    private const string EventType = "PaymentProviderResult.v1";

    private readonly MockWebhookScenarioState _scenarioState;
    private readonly MockWebhookDeliveryChannel _channel;
    private readonly MockPaymentProviderOptions _options;
    private readonly TimeProvider _timeProvider;

    public MockWebhookDispatcher(
        MockWebhookScenarioState scenarioState,
        MockWebhookDeliveryChannel channel,
        MockPaymentProviderOptions options,
        TimeProvider timeProvider)
    {
        _scenarioState = scenarioState;
        _channel = channel;
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task ScheduleCaptureAsync(
        CapturePaymentRequest request,
        MockCaptureDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (decision.IsIdempotentReplay)
        {
            return Task.CompletedTask;
        }

        var result = decision.Outcome switch
        {
            MockCaptureOutcome.Succeeded or MockCaptureOutcome.TimeoutAfterProcessing =>
                new ProviderResult("Succeeded", decision.ProviderReference, null),
            MockCaptureOutcome.Declined =>
                new ProviderResult("DefinitivelyFailed", null, "DECLINED"),
            _ => null
        };

        return result is null
            ? Task.CompletedTask
            : ScheduleAsync(
                "Capture",
                request.PaymentId,
                request.PaymentId,
                null,
                result,
                _scenarioState.GetCaptureScenario(request.PaymentId),
                cancellationToken);
    }

    public Task ScheduleRefundAsync(
        RefundPaymentRequest request,
        MockRefundDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (decision.IsIdempotentReplay)
        {
            return Task.CompletedTask;
        }

        var result = decision.Outcome switch
        {
            MockRefundOutcome.Succeeded or MockRefundOutcome.TimeoutAfterProcessing =>
                new ProviderResult("Succeeded", decision.ProviderReference, null),
            MockRefundOutcome.Rejected =>
                new ProviderResult("DefinitivelyRejected", null, "REFUND_REJECTED"),
            _ => null
        };

        return result is null
            ? Task.CompletedTask
            : ScheduleAsync(
                "Refund",
                request.RefundId,
                request.PaymentId,
                request.RefundId,
                result,
                _scenarioState.GetRefundScenario(request.RefundId),
                cancellationToken);
    }

    private async Task ScheduleAsync(
        string operationType,
        Guid operationId,
        Guid paymentId,
        Guid? refundId,
        ProviderResult result,
        MockWebhookScenario scenario,
        CancellationToken cancellationToken)
    {
        if (scenario == MockWebhookScenario.None
            || !_scenarioState.TryReserveEventId(operationType, operationId, out var eventId))
        {
            return;
        }

        var payload = new MockProviderWebhookRequest(
            eventId,
            EventType,
            operationType,
            paymentId,
            refundId,
            result.Outcome,
            result.ProviderReference,
            result.ErrorCode,
            _timeProvider.GetUtcNow());

        if (scenario == MockWebhookScenario.DelayedWebhook)
        {
            await _channel.EnqueueAsync(
                new QueuedProviderWebhook(payload, _options.DelayedWebhookDelay),
                cancellationToken);
            return;
        }

        if (scenario == MockWebhookScenario.DuplicateWebhook)
        {
            await _channel.EnqueueAsync(
                new QueuedProviderWebhook(payload, TimeSpan.Zero),
                cancellationToken);
            await _channel.EnqueueAsync(
                new QueuedProviderWebhook(payload, _options.DuplicateWebhookDelay),
                cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported webhook scenario '{scenario}'.");
    }

    private sealed record ProviderResult(
        string Outcome,
        string? ProviderReference,
        string? ErrorCode);
}
