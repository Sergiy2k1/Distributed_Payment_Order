using System.Collections.ObjectModel;

namespace PayFlow.Saga.Domain.Checkout;

public sealed class CheckoutSaga
{
    private CheckoutSaga(
        Guid orderId,
        Guid customerId,
        ReadOnlyCollection<CheckoutSagaItem> items,
        string currency,
        decimal totalAmount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        Currency = currency;
        TotalAmount = totalAmount;
        Status = CheckoutSagaStatus.Started;
        StartedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
        DeadlineAtUtc = deadlineAtUtc;
        ReservationId = null;
        ReservationExpiresAtUtc = null;
        PaymentId = null;
        RetryCount = 0;
        NextAttemptAtUtc = null;
        LastTechnicalErrorCode = null;
        LastTechnicalErrorMessage = null;
        Version = 0;
    }

    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public IReadOnlyList<CheckoutSagaItem> Items { get; }
    public string Currency { get; }
    public decimal TotalAmount { get; }
    public CheckoutSagaStatus Status { get; private set; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset DeadlineAtUtc { get; }
    public Guid? ReservationId { get; private set; }
    public DateTimeOffset? ReservationExpiresAtUtc { get; private set; }
    public Guid? PaymentId { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LastTechnicalErrorCode { get; private set; }
    public string? LastTechnicalErrorMessage { get; private set; }
    public long Version { get; private set; }

    public static CheckoutSaga Start(
        Guid orderId,
        Guid customerId,
        IEnumerable<CheckoutSagaItem> items,
        string currency,
        decimal totalAmount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        ValidateIdentity(orderId, nameof(orderId));
        ValidateIdentity(customerId, nameof(customerId));
        ArgumentNullException.ThrowIfNull(items);
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        EnsureUtc(deadlineAtUtc, nameof(deadlineAtUtc));

        if (deadlineAtUtc <= startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadlineAtUtc),
                deadlineAtUtc,
                "Checkout deadline must be later than the start time.");
        }

        var itemArray = items.ToArray();

        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Checkout Saga must contain at least one item.",
                nameof(items));
        }

        var normalizedCurrency =
            CheckoutSagaItem.NormalizeCurrency(currency);

        if (itemArray.Any(
                item => !string.Equals(
                    item.Currency,
                    normalizedCurrency,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "All checkout items must use the Saga currency.",
                nameof(items));
        }

        if (totalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalAmount),
                totalAmount,
                "Total amount must be greater than zero.");
        }

        var calculatedTotal =
            itemArray.Sum(item => item.LineTotal);

        if (calculatedTotal != totalAmount)
        {
            throw new ArgumentException(
                "Total amount must equal the immutable item snapshot total.",
                nameof(totalAmount));
        }

        return new CheckoutSaga(
            orderId,
            customerId,
            Array.AsReadOnly(itemArray),
            normalizedCurrency,
            totalAmount,
            startedAtUtc,
            deadlineAtUtc);
    }

    public static CheckoutSaga Rehydrate(
        Guid orderId,
        Guid customerId,
        IEnumerable<CheckoutSagaItem> items,
        string currency,
        decimal totalAmount,
        CheckoutSagaStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset deadlineAtUtc,
        Guid? reservationId,
        DateTimeOffset? reservationExpiresAtUtc,
        Guid? paymentId,
        int retryCount,
        DateTimeOffset? nextAttemptAtUtc,
        string? lastTechnicalErrorCode,
        string? lastTechnicalErrorMessage,
        long version)
    {
        var saga = Start(
            orderId,
            customerId,
            items,
            currency,
            totalAmount,
            startedAtUtc,
            deadlineAtUtc);

        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                updatedAtUtc,
                "Saga update timestamp cannot be earlier than start time.");
        }

        if (retryCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryCount),
                retryCount,
                "Retry count cannot be negative.");
        }

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Saga version cannot be negative.");
        }

        if (nextAttemptAtUtc is { } nextAttempt)
        {
            EnsureUtc(
                nextAttempt,
                nameof(nextAttemptAtUtc));
        }

        ValidateReservationMetadata(
            reservationId,
            reservationExpiresAtUtc,
            startedAtUtc,
            deadlineAtUtc);

        saga.Status = status;
        saga.UpdatedAtUtc = updatedAtUtc;
        saga.ReservationId = reservationId;
        saga.ReservationExpiresAtUtc =
            reservationExpiresAtUtc;
        saga.PaymentId = paymentId;
        saga.RetryCount = retryCount;
        saga.NextAttemptAtUtc = nextAttemptAtUtc;
        saga.LastTechnicalErrorCode =
            lastTechnicalErrorCode;
        saga.LastTechnicalErrorMessage =
            lastTechnicalErrorMessage;
        saga.Version = version;

        return saga;
    }

    public void BeginInventoryReservation(
        Guid reservationId,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ValidateIdentity(
            reservationId,
            nameof(reservationId));
        EnsureUtc(
            occurredAtUtc,
            nameof(occurredAtUtc));
        EnsureUtc(
            expiresAtUtc,
            nameof(expiresAtUtc));

        if (Status == CheckoutSagaStatus.WaitingForInventory)
        {
            if (ReservationId == reservationId
                && ReservationExpiresAtUtc == expiresAtUtc)
            {
                return;
            }

            throw new InvalidOperationException(
                "Inventory reservation was already started with different metadata.");
        }

        if (Status != CheckoutSagaStatus.Started)
        {
            throw new InvalidOperationException(
                $"Checkout Saga cannot begin inventory reservation from {Status}.");
        }

        if (occurredAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAtUtc),
                occurredAtUtc,
                "Saga transition timestamp cannot be earlier than its current update timestamp.");
        }

        if (occurredAtUtc >= DeadlineAtUtc)
        {
            throw new InvalidOperationException(
                "Checkout deadline has already been reached.");
        }

        if (expiresAtUtc <= occurredAtUtc
            || expiresAtUtc > DeadlineAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                expiresAtUtc,
                "Inventory reservation expiry must be after the transition and no later than the checkout deadline.");
        }

        ReservationId = reservationId;
        ReservationExpiresAtUtc = expiresAtUtc;
        Status = CheckoutSagaStatus.WaitingForInventory;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }


    public void ConfirmInventoryReserved(
        Guid reservationId,
        Guid paymentId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateIdentity(reservationId, nameof(reservationId));
        ValidateIdentity(paymentId, nameof(paymentId));
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (Status == CheckoutSagaStatus.WaitingForPayment)
        {
            if (ReservationId == reservationId
                && PaymentId == paymentId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Inventory reservation was already confirmed with different workflow identities.");
        }

        if (Status != CheckoutSagaStatus.WaitingForInventory)
        {
            throw new InvalidOperationException(
                $"Checkout Saga cannot confirm inventory reservation from {Status}.");
        }

        if (ReservationId != reservationId)
        {
            throw new InvalidOperationException(
                "InventoryReserved reservation identity does not match the persisted Saga reservation.");
        }

        if (occurredAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAtUtc),
                occurredAtUtc,
                "Saga transition timestamp cannot be earlier than its current update timestamp.");
        }

        PaymentId = paymentId;
        Status = CheckoutSagaStatus.WaitingForPayment;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void RejectInventoryReservation(
        Guid reservationId,
        DateTimeOffset occurredAtUtc)
    {
        ValidateIdentity(reservationId, nameof(reservationId));
        EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        if (Status == CheckoutSagaStatus.WaitingForOrderCancellation)
        {
            if (ReservationId == reservationId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Inventory rejection references a different reservation.");
        }

        if (Status != CheckoutSagaStatus.WaitingForInventory)
        {
            throw new InvalidOperationException(
                $"Checkout Saga cannot reject inventory reservation from {Status}.");
        }

        if (ReservationId != reservationId)
        {
            throw new InvalidOperationException(
                "InventoryReservationRejected reservation identity does not match the persisted Saga reservation.");
        }

        if (occurredAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAtUtc),
                occurredAtUtc,
                "Saga transition timestamp cannot be earlier than its current update timestamp.");
        }

        Status = CheckoutSagaStatus.WaitingForOrderCancellation;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    private static void ValidateReservationMetadata(
        Guid? reservationId,
        DateTimeOffset? reservationExpiresAtUtc,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        if (reservationId.HasValue
            != reservationExpiresAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Reservation identity and expiry must either both be set or both be null.");
        }

        if (reservationId is { } id)
        {
            ValidateIdentity(
                id,
                nameof(reservationId));

            var expiresAtUtc =
                reservationExpiresAtUtc!.Value;

            EnsureUtc(
                expiresAtUtc,
                nameof(reservationExpiresAtUtc));

            if (expiresAtUtc <= startedAtUtc
                || expiresAtUtc > deadlineAtUtc)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reservationExpiresAtUtc),
                    expiresAtUtc,
                    "Persisted reservation expiry is outside the Saga deadline.");
            }
        }
    }

    private static void ValidateIdentity(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty.",
                parameterName);
        }
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
