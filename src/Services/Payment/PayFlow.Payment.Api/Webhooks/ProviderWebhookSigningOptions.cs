using System.Text;

namespace PayFlow.Payment.Api.Webhooks;

public sealed class ProviderWebhookSigningOptions
{
    private const int MinimumSecretBytes = 32;

    public ProviderWebhookSigningOptions(
        string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException(
                "ProviderWebhook:SigningSecret is required.");
        }

        if (Encoding.UTF8.GetByteCount(signingSecret)
            < MinimumSecretBytes)
        {
            throw new InvalidOperationException(
                $"ProviderWebhook:SigningSecret must contain at least {MinimumSecretBytes} UTF-8 bytes.");
        }

        SigningSecret = signingSecret;
    }

    public string SigningSecret { get; }
}
