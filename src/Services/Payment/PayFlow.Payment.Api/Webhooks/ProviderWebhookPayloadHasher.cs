using System.Security.Cryptography;
using System.Text.Json;

namespace PayFlow.Payment.Api.Webhooks;

public static class ProviderWebhookPayloadHasher
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static string Compute(
        ProviderWebhookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bytes =
            JsonSerializer.SerializeToUtf8Bytes(
                request,
                SerializerOptions);

        return Convert.ToHexString(
            SHA256.HashData(bytes));
    }
}
