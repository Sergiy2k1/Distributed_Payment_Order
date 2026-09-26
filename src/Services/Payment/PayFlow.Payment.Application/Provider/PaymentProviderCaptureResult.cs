namespace PayFlow.Payment.Application.Provider;

public sealed record PaymentProviderCaptureResult(
    PaymentProviderCaptureOutcome Outcome,
    string? ProviderReference,
    string? ErrorCode)
{
    public static PaymentProviderCaptureResult Succeeded(
        string providerReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerReference);

        return new PaymentProviderCaptureResult(
            PaymentProviderCaptureOutcome.Succeeded,
            providerReference,
            null);
    }

    public static PaymentProviderCaptureResult DefinitivelyFailed(
        string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            errorCode);

        return new PaymentProviderCaptureResult(
            PaymentProviderCaptureOutcome.DefinitivelyFailed,
            null,
            errorCode);
    }

    public static PaymentProviderCaptureResult Ambiguous(
        string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            errorCode);

        return new PaymentProviderCaptureResult(
            PaymentProviderCaptureOutcome.Ambiguous,
            null,
            errorCode);
    }
}
