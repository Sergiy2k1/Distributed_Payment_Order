namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed record QueuedProviderWebhook(
    MockProviderWebhookRequest Payload,
    TimeSpan Delay);
