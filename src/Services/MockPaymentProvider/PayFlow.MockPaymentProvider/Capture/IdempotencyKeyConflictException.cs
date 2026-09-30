namespace PayFlow.MockPaymentProvider.Capture;

public sealed class IdempotencyKeyConflictException
    : Exception
{
    public IdempotencyKeyConflictException(
        string idempotencyKey)
        : base(
            $"Idempotency key '{idempotencyKey}' was already used for a different capture request.")
    {
        IdempotencyKey = idempotencyKey;
    }

    public string IdempotencyKey { get; }
}
