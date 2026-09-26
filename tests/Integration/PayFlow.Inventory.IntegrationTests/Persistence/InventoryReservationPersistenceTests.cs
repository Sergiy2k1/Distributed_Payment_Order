using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Domain.Reservations;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;
using PayFlow.Inventory.IntegrationTests.Infrastructure;

namespace PayFlow.Inventory.IntegrationTests.Persistence;

public sealed class InventoryReservationPersistenceTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ReservationRoundTripsThroughPostgreSql()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var createdAtUtc =
            new DateTimeOffset(
                2026,
                9,
                26,
                20,
                0,
                0,
                TimeSpan.Zero);

        var reservation =
            InventoryReservation.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [
                    InventoryReservationItem.Create(
                        "SKU-001",
                        2),
                    InventoryReservationItem.Create(
                        "SKU-002",
                        1)
                ],
                createdAtUtc,
                createdAtUtc.AddMinutes(30));

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            await new InventoryReservationRepository(
                    dbContext)
                .AddAsync(
                    reservation,
                    cancellationToken);

            await dbContext
                .SaveChangesAsync(cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var loaded =
            await new InventoryReservationRepository(
                    verificationDbContext)
                .GetByIdAsync(
                    reservation.ReservationId,
                    cancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(
            InventoryReservationStatus.Pending,
            loaded.Status);
        Assert.Equal(
            reservation.OrderId,
            loaded.OrderId);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(0, loaded.Version);

        Assert.Equal(
            1,
            await verificationDbContext.Reservations
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.ReservationId
                        == reservation.ReservationId,
                    cancellationToken));
    }
}
