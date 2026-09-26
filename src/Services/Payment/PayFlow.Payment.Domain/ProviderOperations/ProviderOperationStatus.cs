namespace PayFlow.Payment.Domain.ProviderOperations;

public enum ProviderOperationStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    DefinitivelyFailed = 3,
    Ambiguous = 4
}
