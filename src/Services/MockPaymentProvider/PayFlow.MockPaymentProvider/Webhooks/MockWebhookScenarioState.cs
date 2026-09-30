using System.Collections.Concurrent;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed class MockWebhookScenarioState
{
    private readonly ConcurrentDictionary<string, MockWebhookScenario> _scenarios = new();
    private readonly ConcurrentDictionary<string, Guid> _scheduledEvents = new();

    public void ConfigureCapture(Guid paymentId, MockWebhookScenario scenario) =>
        Configure(Key("Capture", paymentId), scenario);

    public void ConfigureRefund(Guid refundId, MockWebhookScenario scenario) =>
        Configure(Key("Refund", refundId), scenario);

    public void ResetCapture(Guid paymentId) => Reset(Key("Capture", paymentId));
    public void ResetRefund(Guid refundId) => Reset(Key("Refund", refundId));

    public MockWebhookScenario GetCaptureScenario(Guid paymentId) =>
        Get(Key("Capture", paymentId));

    public MockWebhookScenario GetRefundScenario(Guid refundId) =>
        Get(Key("Refund", refundId));

    public bool TryReserveEventId(string operationType, Guid operationId, out Guid eventId)
    {
        var key = Key(operationType, operationId);
        var candidate = Guid.NewGuid();
        eventId = _scheduledEvents.GetOrAdd(key, candidate);
        return eventId == candidate;
    }

    private void Configure(string key, MockWebhookScenario scenario)
    {
        _scenarios[key] = scenario;
        _scheduledEvents.TryRemove(key, out _);
    }

    private void Reset(string key)
    {
        _scenarios.TryRemove(key, out _);
        _scheduledEvents.TryRemove(key, out _);
    }

    private MockWebhookScenario Get(string key) =>
        _scenarios.GetValueOrDefault(key, MockWebhookScenario.None);

    private static string Key(string operationType, Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation id cannot be empty.", nameof(operationId));
        }

        return $"{operationType}:{operationId:D}";
    }
}
