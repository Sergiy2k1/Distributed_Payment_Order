namespace PayFlow.Payment.Application.Provider;

public enum PaymentProviderRefundOutcome
{
    Succeeded = 0,
    DefinitivelyRejected = 1,
    Ambiguous = 2
}
