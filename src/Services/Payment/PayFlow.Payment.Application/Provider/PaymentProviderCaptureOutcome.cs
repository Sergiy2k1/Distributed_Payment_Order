namespace PayFlow.Payment.Application.Provider;

public enum PaymentProviderCaptureOutcome
{
    Succeeded = 0,
    DefinitivelyFailed = 1,
    Ambiguous = 2
}
