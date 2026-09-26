using PayFlow.Inventory.Domain.Reservations;

namespace PayFlow.Inventory.Application.Abstractions;

public interface IInventoryReservationRepository
{
    Task AddAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default);

    Task<InventoryReservation?> GetByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default);
}
