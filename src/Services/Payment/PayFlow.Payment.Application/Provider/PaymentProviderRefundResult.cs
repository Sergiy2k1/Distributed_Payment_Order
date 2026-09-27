namespace PayFlow.Payment.Application.Provider;

public sealed record PaymentProviderRefundResult(
    PaymentProviderRefundOutcome Outcome,
    string? ProviderReference,
    string? ErrorCode)
{
    public static PaymentProviderRefundResult Succeeded(
        string providerReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);

        return new(
            PaymentProviderRefundOutcome.Succeeded,
            providerReference,
            null);
    }

    public static PaymentProviderRefundResult DefinitivelyRejected(
        string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return new(
            PaymentProviderRefundOutcome.DefinitivelyRejected,
            null,
            errorCode);
    }

    public static PaymentProviderRefundResult Ambiguous(
        string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return new(
            PaymentProviderRefundOutcome.Ambiguous,
            null,
            errorCode);
    }
}
