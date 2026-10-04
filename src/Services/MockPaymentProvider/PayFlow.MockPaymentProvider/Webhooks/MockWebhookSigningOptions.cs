using System.Text;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed class MockWebhookSigningOptions
{
    private const int MinimumSecretBytes = 32;

    public MockWebhookSigningOptions(
        string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException(
                "MockPaymentProvider:WebhookSigningSecret is required.");
        }

        if (Encoding.UTF8.GetByteCount(signingSecret)
            < MinimumSecretBytes)
        {
            throw new InvalidOperationException(
                $"MockPaymentProvider:WebhookSigningSecret must contain at least {MinimumSecretBytes} UTF-8 bytes.");
        }

        SigningSecret = signingSecret;
    }

    public string SigningSecret { get; }
}
