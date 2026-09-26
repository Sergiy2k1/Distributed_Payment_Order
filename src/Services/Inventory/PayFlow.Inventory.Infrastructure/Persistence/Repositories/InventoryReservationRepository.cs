using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Reservations;
using PayFlow.Inventory.Infrastructure.Persistence.Mappers;

namespace PayFlow.Inventory.Infrastructure.Persistence.Repositories;

public sealed class InventoryReservationRepository
    : IInventoryReservationRepository
{
    private readonly InventoryDbContext _dbContext;

    public InventoryReservationRepository(
        InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await _dbContext.Reservations
            .AddAsync(
                InventoryReservationEntityMapper.ToEntity(
                    reservation),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<InventoryReservation?> GetByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Reservation ID cannot be empty.",
                nameof(reservationId));
        }

        var entity = await _dbContext.Reservations
            .Include(reservation => reservation.Items)
            .SingleOrDefaultAsync(
                reservation =>
                    reservation.ReservationId
                    == reservationId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : InventoryReservationEntityMapper.ToDomain(
                entity);
    }

    public Task ApplyAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = _dbContext.Reservations.Local
            .SingleOrDefault(
                tracked =>
                    tracked.ReservationId
                    == reservation.ReservationId)
            ?? throw new InvalidOperationException(
                "Inventory reservation must be loaded by this repository before applying a transition.");

        entity.Status =
            reservation.Status.ToString();
        entity.UpdatedAtUtc =
            reservation.UpdatedAtUtc;
        entity.RejectionReasonCode =
            reservation.RejectionReasonCode;
        entity.Version =
            reservation.Version;

        return Task.CompletedTask;
    }
}
