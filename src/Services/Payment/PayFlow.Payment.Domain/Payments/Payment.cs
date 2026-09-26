namespace PayFlow.Payment.Domain.Payments;

public sealed class Payment
{
    private Payment(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        Currency = NormalizeCurrency(currency);
        Status = PaymentStatus.Created;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        CapturedAtUtc = null;
        FailureReasonCode = null;
        Version = 0;
    }

    public Guid PaymentId { get; }
    public Guid OrderId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CapturedAtUtc { get; private set; }
    public string? FailureReasonCode { get; private set; }
    public long Version { get; private set; }

    public static Payment Create(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        ValidateIdentity(paymentId, nameof(paymentId));
        ValidateIdentity(orderId, nameof(orderId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Payment amount must be greater than zero.");
        }

        return new Payment(
            paymentId,
            orderId,
            amount,
            currency,
            createdAtUtc);
    }

    public static Payment Rehydrate(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string currency,
        PaymentStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? capturedAtUtc,
        string? failureReasonCode,
        long version)
    {
        var payment = Create(
            paymentId,
            orderId,
            amount,
            currency,
            createdAtUtc);

        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(
            version);

        if (capturedAtUtc is { } captured)
        {
            EnsureUtc(captured, nameof(capturedAtUtc));
        }

        if (status == PaymentStatus.Captured
            && capturedAtUtc is null)
        {
            throw new InvalidOperationException(
                "Captured payment must have CapturedAtUtc.");
        }

        if (status != PaymentStatus.Captured
            && capturedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "Only captured payment may have CapturedAtUtc.");
        }

        if (status == PaymentStatus.Failed
            && string.IsNullOrWhiteSpace(failureReasonCode))
        {
            throw new InvalidOperationException(
                "Failed payment must have a failure reason.");
        }

        if (status != PaymentStatus.Failed
            && failureReasonCode is not null)
        {
            throw new InvalidOperationException(
                "Only failed payment may have a failure reason.");
        }

        payment.Status = status;
        payment.UpdatedAtUtc = updatedAtUtc;
        payment.CapturedAtUtc = capturedAtUtc;
        payment.FailureReasonCode = failureReasonCode;
        payment.Version = version;

        return payment;
    }

    public void StartProcessing(DateTimeOffset occurredAtUtc)
    {
        EnsureTransitionTime(occurredAtUtc);

        if (Status == PaymentStatus.Processing)
        {
            return;
        }

        if (Status != PaymentStatus.Created)
        {
            throw new InvalidOperationException(
                $"Payment cannot start processing from {Status}.");
        }

        Status = PaymentStatus.Processing;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void MarkCaptured(DateTimeOffset capturedAtUtc)
    {
        EnsureTransitionTime(capturedAtUtc);

        if (Status == PaymentStatus.Captured)
        {
            return;
        }

        if (Status != PaymentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Payment cannot be captured from {Status}.");
        }

        Status = PaymentStatus.Captured;
        CapturedAtUtc = capturedAtUtc;
        UpdatedAtUtc = capturedAtUtc;
        FailureReasonCode = null;
        Version++;
    }

    public void MarkFailed(
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        EnsureTransitionTime(occurredAtUtc);

        if (Status == PaymentStatus.Failed)
        {
            if (string.Equals(
                    FailureReasonCode,
                    reasonCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Payment was already failed with a different reason.");
        }

        if (Status != PaymentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Payment cannot fail from {Status}.");
        }

        Status = PaymentStatus.Failed;
        FailureReasonCode = reasonCode;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    private void EnsureTransitionTime(DateTimeOffset value)
    {
        EnsureUtc(value, nameof(value));

        if (value < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Transition timestamp cannot be earlier than current Payment state.");
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new ArgumentException(
                "Currency must be a 3-letter code.",
                nameof(currency));
        }

        return normalized;
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
