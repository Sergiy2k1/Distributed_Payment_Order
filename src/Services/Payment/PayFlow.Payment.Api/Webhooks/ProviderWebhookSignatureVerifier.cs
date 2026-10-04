using System.Security.Cryptography;
using System.Text;

namespace PayFlow.Payment.Api.Webhooks;

public static class ProviderWebhookSignatureVerifier
{
    public const string HeaderName = "X-PayFlow-Signature";
    private const string Prefix = "sha256=";

    public static bool IsValid(
        ReadOnlySpan<byte> payload,
        string? signatureHeader,
        string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith(
                Prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        Span<byte> providedSignature =
            stackalloc byte[32];

        if (!Convert.TryFromHexString(
                signatureHeader.AsSpan(Prefix.Length),
                providedSignature,
                out var bytesWritten)
            || bytesWritten != providedSignature.Length)
        {
            return false;
        }

        using var hmac =
            new HMACSHA256(
                Encoding.UTF8.GetBytes(signingSecret));
        var expectedSignature =
            hmac.ComputeHash(payload.ToArray());

        return CryptographicOperations.FixedTimeEquals(
            expectedSignature,
            providedSignature);
    }
}
