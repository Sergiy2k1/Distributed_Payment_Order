using PayFlow.Inventory.Domain.Reservations;

namespace PayFlow.Inventory.UnitTests.Reservations;

public sealed class InventoryReservationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 19, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAtUtc =
        CreatedAtUtc.AddMinutes(30);

    [Fact]
    public void CreateBuildsPendingReservation()
    {
        var reservationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var reservation =
            InventoryReservation.Create(
                reservationId,
                orderId,
                CreateItems(),
                CreatedAtUtc,
                ExpiresAtUtc);

        Assert.Equal(
            reservationId,
            reservation.ReservationId);
        Assert.Equal(
            orderId,
            reservation.OrderId);
        Assert.Equal(
            InventoryReservationStatus.Pending,
            reservation.Status);
        Assert.Equal(0, reservation.Version);
        Assert.Equal(2, reservation.Items.Count);
        Assert.Null(
            reservation.RejectionReasonCode);
    }

    [Fact]
    public void MarkReservedTransitionsPendingToReserved()
    {
        var reservation = CreateReservation();

        reservation.MarkReserved(
            CreatedAtUtc.AddSeconds(1));

        Assert.Equal(
            InventoryReservationStatus.Reserved,
            reservation.Status);
        Assert.Equal(1, reservation.Version);
    }

    [Fact]
    public void RejectIsIdempotentForSameReason()
    {
        var reservation = CreateReservation();
        var occurredAtUtc =
            CreatedAtUtc.AddSeconds(1);

        reservation.Reject(
            "INSUFFICIENT_STOCK",
            occurredAtUtc);
        reservation.Reject(
            "INSUFFICIENT_STOCK",
            occurredAtUtc);

        Assert.Equal(
            InventoryReservationStatus.Rejected,
            reservation.Status);
        Assert.Equal(
            "INSUFFICIENT_STOCK",
            reservation.RejectionReasonCode);
        Assert.Equal(1, reservation.Version);
    }

    [Fact]
    public void ConsumeTransitionsReservedToConsumed()
    {
        var reservation = CreateReservation();

        reservation.MarkReserved(
            CreatedAtUtc.AddSeconds(1));
        reservation.Consume(
            CreatedAtUtc.AddSeconds(2));

        Assert.Equal(
            InventoryReservationStatus.Consumed,
            reservation.Status);
        Assert.Equal(2, reservation.Version);
    }

    [Fact]
    public void ReleaseTransitionsReservedToReleased()
    {
        var reservation = CreateReservation();

        reservation.MarkReserved(
            CreatedAtUtc.AddSeconds(1));
        reservation.Release(
            CreatedAtUtc.AddSeconds(2));

        Assert.Equal(
            InventoryReservationStatus.Released,
            reservation.Status);
        Assert.Equal(2, reservation.Version);
    }

    [Fact]
    public void ExpireRejectsTimeBeforeDeadline()
    {
        var reservation = CreateReservation();

        reservation.MarkReserved(
            CreatedAtUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(
            () => reservation.Expire(
                ExpiresAtUtc.AddSeconds(-1)));
    }

    [Fact]
    public void CreateRejectsDuplicateSkuEntries()
    {
        var items = new[]
        {
            InventoryReservationItem.Create(
                "SKU-001",
                1),
            InventoryReservationItem.Create(
                "SKU-001",
                2)
        };

        Assert.Throws<ArgumentException>(
            () => InventoryReservation.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                items,
                CreatedAtUtc,
                ExpiresAtUtc));
    }

    private static InventoryReservation
        CreateReservation()
    {
        return InventoryReservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateItems(),
            CreatedAtUtc,
            ExpiresAtUtc);
    }

    private static InventoryReservationItem[]
        CreateItems()
    {
        return
        [
            InventoryReservationItem.Create(
                "SKU-001",
                1),
            InventoryReservationItem.Create(
                "SKU-002",
                2)
        ];
    }
}
