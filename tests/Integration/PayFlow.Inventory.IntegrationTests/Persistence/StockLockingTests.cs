using Microsoft.EntityFrameworkCore;
using PayFlow.Inventory.Domain.Stock;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;
using PayFlow.Inventory.IntegrationTests.Infrastructure;

namespace PayFlow.Inventory.IntegrationTests.Persistence;

public sealed class StockLockingTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ConcurrentReservationsNeverOversellSingleSku()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var skuId =
            $"SKU-HOT-{Guid.NewGuid():N}";

        await SeedStockAsync(
            skuId,
            onHand: 10,
            cancellationToken);

        var attempts = Enumerable.Range(0, 100)
            .Select(
                _ => TryReserveOneAsync(
                    skuId,
                    cancellationToken))
            .ToArray();

        var results =
            await Task.WhenAll(attempts);

        Assert.Equal(
            10,
            results.Count(
                static reserved => reserved));

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var persisted =
            await verificationDbContext.StockItems
                .AsNoTracking()
                .SingleAsync(
                    stock => stock.SkuId == skuId,
                    cancellationToken);

        Assert.Equal(10, persisted.OnHand);
        Assert.Equal(10, persisted.Reserved);
        Assert.Equal(
            0,
            persisted.OnHand - persisted.Reserved);
    }

    [Fact]
    public async Task MultiSkuLocksAreReturnedInCanonicalOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var prefix =
            Guid.NewGuid().ToString("N");

        var skuA = $"A-{prefix}";
        var skuB = $"B-{prefix}";
        var skuC = $"C-{prefix}";

        await SeedStockAsync(
            skuA,
            5,
            cancellationToken);
        await SeedStockAsync(
            skuB,
            5,
            cancellationToken);
        await SeedStockAsync(
            skuC,
            5,
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();
        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        var repository =
            new StockRepository(dbContext);

        var locked =
            await repository.LockBySkuIdsAsync(
                [skuC, skuA, skuB],
                cancellationToken);

        Assert.Equal(
            [skuA, skuB, skuC],
            locked
                .Select(stock => stock.SkuId)
                .ToArray());

        await transaction.RollbackAsync(
            cancellationToken);
    }

    private async Task<bool> TryReserveOneAsync(
        string skuId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();
        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        var repository =
            new StockRepository(dbContext);

        var stock =
            (await repository.LockBySkuIdsAsync(
                [skuId],
                cancellationToken))
            .Single();

        if (stock.Available < 1)
        {
            await transaction.CommitAsync(
                cancellationToken);

            return false;
        }

        stock.Reserve(1);

        await repository.ApplyAsync(
            [stock],
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return true;
    }

    private async Task SeedStockAsync(
        string skuId,
        int onHand,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        await new StockRepository(dbContext)
            .AddAsync(
                StockItem.Create(
                    skuId,
                    onHand),
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
