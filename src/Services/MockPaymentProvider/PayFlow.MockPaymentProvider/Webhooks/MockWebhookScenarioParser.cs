namespace PayFlow.MockPaymentProvider.Webhooks;

public static class MockWebhookScenarioParser
{
    public static bool TryParse(
        string? value,
        out MockWebhookScenario scenario)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "none":
                scenario = MockWebhookScenario.None;
                return true;
            case "delayed_webhook":
                scenario = MockWebhookScenario.DelayedWebhook;
                return true;
            case "duplicate_webhook":
                scenario = MockWebhookScenario.DuplicateWebhook;
                return true;
            default:
                scenario = default;
                return false;
        }
    }
}
