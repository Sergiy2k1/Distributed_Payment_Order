using System.Security.Cryptography;
using System.Text;

namespace PayFlow.Payment.Api.Webhooks;

public static class ProviderWebhookSignatureVerifier
{
    public const string HeaderName = "X-PayFlow-Signature";
    private const string Prefix = "sha256=";
    private const int Sha256HexLength = 64;

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

        var signatureHex =
            signatureHeader[Prefix.Length..];

        if (signatureHex.Length != Sha256HexLength)
        {
            return false;
        }

        byte[] providedSignature;

        try
        {
            providedSignature =
                Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
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
