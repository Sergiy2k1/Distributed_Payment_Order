using PayFlow.Inventory.Domain.Reservations;
using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Mappers;

public static class InventoryReservationEntityMapper
{
    public static InventoryReservation ToDomain(
        InventoryReservationEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!Enum.TryParse<InventoryReservationStatus>(
                entity.Status,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted Inventory reservation status '{entity.Status}' is invalid.");
        }

        var items = entity.Items
            .OrderBy(item => item.Position)
            .Select(
                item => InventoryReservationItem.Create(
                    item.SkuId,
                    item.Quantity))
            .ToArray();

        return InventoryReservation.Rehydrate(
            entity.ReservationId,
            entity.OrderId,
            items,
            status,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.ExpiresAtUtc,
            entity.RejectionReasonCode,
            entity.Version);
    }

    public static InventoryReservationEntity ToEntity(
        InventoryReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new InventoryReservationEntity
        {
            ReservationId = reservation.ReservationId,
            OrderId = reservation.OrderId,
            Status = reservation.Status.ToString(),
            CreatedAtUtc = reservation.CreatedAtUtc,
            UpdatedAtUtc = reservation.UpdatedAtUtc,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            RejectionReasonCode =
                reservation.RejectionReasonCode,
            Version = reservation.Version,
            Items = reservation.Items
                .Select(
                    (item, position) =>
                        new InventoryReservationItemEntity
                        {
                            ReservationId =
                                reservation.ReservationId,
                            Position = position,
                            SkuId = item.SkuId,
                            Quantity = item.Quantity
                        })
                .ToList()
        };
    }
}
