namespace PayFlow.MockPaymentProvider.Webhooks;

public enum MockWebhookScenario
{
    None = 0,
    DelayedWebhook = 1,
    DuplicateWebhook = 2
}
