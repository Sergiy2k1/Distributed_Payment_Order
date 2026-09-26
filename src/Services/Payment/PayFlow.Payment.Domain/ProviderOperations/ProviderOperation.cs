namespace PayFlow.Payment.Domain.ProviderOperations;

public sealed class ProviderOperation
{
    private ProviderOperation(
        Guid providerOperationId,
        Guid businessOperationId,
        string operationType,
        string providerIdempotencyKey,
        DateTimeOffset createdAtUtc)
    {
        ProviderOperationId = providerOperationId;
        BusinessOperationId = businessOperationId;
        OperationType = operationType;
        ProviderIdempotencyKey = providerIdempotencyKey;
        Status = ProviderOperationStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        LastAttemptAtUtc = null;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        ProviderReference = null;
        Version = 0;
    }

    public Guid ProviderOperationId { get; }
    public Guid BusinessOperationId { get; }
    public string OperationType { get; }
    public string ProviderIdempotencyKey { get; }
    public ProviderOperationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? ProviderReference { get; private set; }
    public long Version { get; private set; }

    public static ProviderOperation CreateCapture(
        Guid providerOperationId,
        Guid paymentId,
        DateTimeOffset createdAtUtc)
    {
        ValidateIdentity(providerOperationId, nameof(providerOperationId));
        ValidateIdentity(paymentId, nameof(paymentId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new ProviderOperation(
            providerOperationId,
            paymentId,
            "Capture",
            $"payment:{paymentId:D}:capture:v1",
            createdAtUtc);
    }

    public void BeginAttempt(DateTimeOffset attemptedAtUtc)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));

        if (Status is ProviderOperationStatus.Succeeded
            or ProviderOperationStatus.DefinitivelyFailed)
        {
            throw new InvalidOperationException(
                $"Terminal provider operation cannot begin another attempt from {Status}.");
        }

        if (attemptedAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptedAtUtc));
        }

        checked
        {
            AttemptCount++;
        }

        Status = ProviderOperationStatus.Processing;
        LastAttemptAtUtc = attemptedAtUtc;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        UpdatedAtUtc = attemptedAtUtc;
        Version++;
    }

    public void MarkSucceeded(
        string providerReference,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (Status == ProviderOperationStatus.Succeeded)
        {
            if (string.Equals(
                    ProviderReference,
                    providerReference,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Provider operation already succeeded with another reference.");
        }

        if (Status is not (
            ProviderOperationStatus.Processing
            or ProviderOperationStatus.Ambiguous))
        {
            throw new InvalidOperationException(
                $"Provider operation cannot succeed from {Status}.");
        }

        ProviderReference = providerReference;
        Status = ProviderOperationStatus.Succeeded;
        UpdatedAtUtc = occurredAtUtc;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        Version++;
    }

    public void MarkDefinitivelyFailed(
        string errorCode,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (Status == ProviderOperationStatus.DefinitivelyFailed)
        {
            if (string.Equals(
                    LastErrorCode,
                    errorCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Provider operation already failed with another reason.");
        }

        if (Status is not (
            ProviderOperationStatus.Processing
            or ProviderOperationStatus.Ambiguous))
        {
            throw new InvalidOperationException(
                $"Provider operation cannot fail from {Status}.");
        }

        Status = ProviderOperationStatus.DefinitivelyFailed;
        LastErrorCode = errorCode;
        UpdatedAtUtc = occurredAtUtc;
        NextAttemptAtUtc = null;
        Version++;
    }

    public void MarkAmbiguous(
        string errorCode,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset nextAttemptAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));

        if (nextAttemptAtUtc <= occurredAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAtUtc));
        }

        if (Status != ProviderOperationStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Only an active provider attempt can become ambiguous from {Status}.");
        }

        Status = ProviderOperationStatus.Ambiguous;
        LastErrorCode = errorCode;
        NextAttemptAtUtc = nextAttemptAtUtc;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    private static void ValidateIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty.",
                parameterName);
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
