using System.Collections.ObjectModel;

namespace PayFlow.Inventory.Domain.Reservations;

public sealed class InventoryReservation
{
    private InventoryReservation(
        Guid reservationId,
        Guid orderId,
        ReadOnlyCollection<InventoryReservationItem> items,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        Items = items;
        Status = InventoryReservationStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        RejectionReasonCode = null;
        Version = 0;
    }

    public Guid ReservationId { get; }

    public Guid OrderId { get; }

    public IReadOnlyList<InventoryReservationItem> Items { get; }

    public InventoryReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public string? RejectionReasonCode { get; private set; }

    public long Version { get; private set; }

    public static InventoryReservation Create(
        Guid reservationId,
        Guid orderId,
        IEnumerable<InventoryReservationItem> items,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ValidateIdentity(
            reservationId,
            nameof(reservationId));
        ValidateIdentity(
            orderId,
            nameof(orderId));
        ArgumentNullException.ThrowIfNull(items);
        EnsureUtc(
            createdAtUtc,
            nameof(createdAtUtc));
        EnsureUtc(
            expiresAtUtc,
            nameof(expiresAtUtc));

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                expiresAtUtc,
                "Reservation expiry must be after creation.");
        }

        var itemArray = items.ToArray();

        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Reservation must contain at least one item.",
                nameof(items));
        }

        if (itemArray
            .GroupBy(
                static item => item.SkuId,
                StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Reservation cannot contain duplicate SKU entries.",
                nameof(items));
        }

        return new InventoryReservation(
            reservationId,
            orderId,
            Array.AsReadOnly(itemArray),
            createdAtUtc,
            expiresAtUtc);
    }

    public void MarkReserved(
        DateTimeOffset occurredAtUtc)
    {
        EnsureTransitionTime(occurredAtUtc);

        if (Status == InventoryReservationStatus.Reserved)
        {
            return;
        }

        EnsureCurrentState(
            InventoryReservationStatus.Pending,
            nameof(MarkReserved));

        if (occurredAtUtc >= ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Expired reservation cannot be marked as reserved.");
        }

        Status = InventoryReservationStatus.Reserved;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void Reject(
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            reasonCode);

        if (reasonCode.Length > 128)
        {
            throw new ArgumentException(
                "Reason code cannot exceed 128 characters.",
                nameof(reasonCode));
        }

        EnsureTransitionTime(occurredAtUtc);

        if (Status == InventoryReservationStatus.Rejected)
        {
            if (string.Equals(
                    RejectionReasonCode,
                    reasonCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Reservation was already rejected with a different reason.");
        }

        EnsureCurrentState(
            InventoryReservationStatus.Pending,
            nameof(Reject));

        RejectionReasonCode = reasonCode;
        Status = InventoryReservationStatus.Rejected;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void Consume(
        DateTimeOffset occurredAtUtc)
    {
        EnsureTransitionTime(occurredAtUtc);

        if (Status == InventoryReservationStatus.Consumed)
        {
            return;
        }

        EnsureCurrentState(
            InventoryReservationStatus.Reserved,
            nameof(Consume));

        Status = InventoryReservationStatus.Consumed;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void Release(
        DateTimeOffset occurredAtUtc)
    {
        EnsureTransitionTime(occurredAtUtc);

        if (Status == InventoryReservationStatus.Released)
        {
            return;
        }

        EnsureCurrentState(
            InventoryReservationStatus.Reserved,
            nameof(Release));

        Status = InventoryReservationStatus.Released;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    public void Expire(
        DateTimeOffset occurredAtUtc)
    {
        EnsureTransitionTime(occurredAtUtc);

        if (Status == InventoryReservationStatus.Expired)
        {
            return;
        }

        EnsureCurrentState(
            InventoryReservationStatus.Reserved,
            nameof(Expire));

        if (occurredAtUtc < ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Reservation cannot expire before its deadline.");
        }

        Status = InventoryReservationStatus.Expired;
        UpdatedAtUtc = occurredAtUtc;
        Version++;
    }

    private void EnsureCurrentState(
        InventoryReservationStatus expected,
        string operation)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Inventory reservation cannot execute {operation} from state '{Status}'.");
        }
    }

    private void EnsureTransitionTime(
        DateTimeOffset occurredAtUtc)
    {
        EnsureUtc(
            occurredAtUtc,
            nameof(occurredAtUtc));

        if (occurredAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAtUtc),
                occurredAtUtc,
                "Transition timestamp cannot be earlier than the current reservation update timestamp.");
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
