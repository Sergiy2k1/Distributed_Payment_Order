namespace PayFlow.Payment.Domain.ProviderOperations;

public sealed class ProviderOperation
{
    private ProviderOperation(
        Guid providerOperationId,
        Guid businessOperationId,
        string operationType,
        string providerIdempotencyKey,
        DateTimeOffset createdAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null,
        string? traceParent = null)
    {
        ProviderOperationId = providerOperationId;
        BusinessOperationId = businessOperationId;
        OperationType = operationType;
        ProviderIdempotencyKey = providerIdempotencyKey;
        CorrelationId = correlationId;
        CausationId = causationId;
        TraceParent = traceParent;
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
    public Guid? CorrelationId { get; }
    public Guid? CausationId { get; }
    public string? TraceParent { get; }
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

    public static ProviderOperation CreateCapture(
        Guid providerOperationId,
        Guid paymentId,
        DateTimeOffset createdAtUtc,
        Guid correlationId,
        Guid causationId,
        string? traceParent)
    {
        ValidateIdentity(providerOperationId, nameof(providerOperationId));
        ValidateIdentity(paymentId, nameof(paymentId));
        ValidateIdentity(correlationId, nameof(correlationId));
        ValidateIdentity(causationId, nameof(causationId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new ProviderOperation(
            providerOperationId,
            paymentId,
            "Capture",
            $"payment:{paymentId:D}:capture:v1",
            createdAtUtc,
            correlationId,
            causationId,
            traceParent);
    }

    public static ProviderOperation CreateRefund(
        Guid refundId,
        Guid paymentId,
        DateTimeOffset createdAtUtc,
        Guid correlationId,
        Guid causationId,
        string? traceParent)
    {
        ValidateIdentity(refundId, nameof(refundId));
        ValidateIdentity(paymentId, nameof(paymentId));
        ValidateIdentity(correlationId, nameof(correlationId));
        ValidateIdentity(causationId, nameof(causationId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new ProviderOperation(
            refundId,
            paymentId,
            "Refund",
            $"payment:{paymentId:D}:refund:{refundId:D}:v1",
            createdAtUtc,
            correlationId,
            causationId,
            traceParent);
    }

    public static ProviderOperation Rehydrate(
        Guid providerOperationId,
        Guid businessOperationId,
        string operationType,
        string providerIdempotencyKey,
        ProviderOperationStatus status,
        int attemptCount,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? lastAttemptAtUtc,
        DateTimeOffset? nextAttemptAtUtc,
        string? lastErrorCode,
        string? providerReference,
        Guid? correlationId,
        Guid? causationId,
        string? traceParent,
        long version)
    {
        ValidateIdentity(
            providerOperationId,
            nameof(providerOperationId));
        ValidateIdentity(
            businessOperationId,
            nameof(businessOperationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIdempotencyKey);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        ArgumentOutOfRangeException.ThrowIfNegative(
            attemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(
            version);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            updatedAtUtc,
            createdAtUtc);

        if (lastAttemptAtUtc is { } lastAttempt)
        {
            EnsureUtc(lastAttempt, nameof(lastAttemptAtUtc));
        }

        if (nextAttemptAtUtc is { } nextAttempt)
        {
            EnsureUtc(nextAttempt, nameof(nextAttemptAtUtc));
        }

        var operation =
            new ProviderOperation(
                providerOperationId,
                businessOperationId,
                operationType,
                providerIdempotencyKey,
                createdAtUtc,
                correlationId,
                causationId,
                traceParent);

        operation.Status = status;
        operation.AttemptCount = attemptCount;
        operation.UpdatedAtUtc = updatedAtUtc;
        operation.LastAttemptAtUtc = lastAttemptAtUtc;
        operation.NextAttemptAtUtc = nextAttemptAtUtc;
        operation.LastErrorCode = lastErrorCode;
        operation.ProviderReference = providerReference;
        operation.Version = version;

        return operation;
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

        ArgumentOutOfRangeException.ThrowIfLessThan(
            attemptedAtUtc,
            UpdatedAtUtc);

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

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            nextAttemptAtUtc,
            occurredAtUtc);

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
