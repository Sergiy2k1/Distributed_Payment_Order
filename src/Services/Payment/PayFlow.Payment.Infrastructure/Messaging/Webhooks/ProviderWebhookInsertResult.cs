namespace PayFlow.Payment.Infrastructure.Messaging.Webhooks;

public enum ProviderWebhookInsertResult
{
    Inserted = 0,
    Duplicate = 1,
    Conflict = 2
}
