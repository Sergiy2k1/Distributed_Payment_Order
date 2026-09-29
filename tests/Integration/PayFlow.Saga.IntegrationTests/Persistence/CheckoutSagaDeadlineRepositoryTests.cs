using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Persistence;

public sealed class CheckoutSagaDeadlineRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 9, 29, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetOverdueActiveReturnsOnlyExpiredNonTerminalSagas()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var overdueActive =
            CreateWaitingForInventorySaga(
                NowUtc.AddHours(-1),
                NowUtc.AddMinutes(-10));

        var futureActive =
            CreateWaitingForInventorySaga(
                NowUtc.AddMinutes(-5),
                NowUtc.AddMinutes(20));

        var completedOverdue =
            CreateCompletedSaga(
                NowUtc.AddHours(-2),
                NowUtc.AddMinutes(-30));

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(
                    dbContext);

            await repository.AddAsync(
                overdueActive,
                cancellationToken);
            await repository.AddAsync(
                futureActive,
                cancellationToken);
            await repository.AddAsync(
                completedOverdue,
                cancellationToken);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var queryDbContext =
            fixture.CreateDbContext();

        var actual =
            await new CheckoutSagaRepository(
                    queryDbContext)
                .GetOverdueActiveAsync(
                    NowUtc,
                    batchSize: 1000,
                    cancellationToken);

        var saga =
            Assert.Single(
                actual,
                candidate =>
                    candidate.OrderId
                    == overdueActive.OrderId);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForInventory,
            saga.Status);
        Assert.True(
            saga.DeadlineAtUtc <= NowUtc);

        Assert.DoesNotContain(
            actual,
            candidate =>
                candidate.OrderId
                == futureActive.OrderId);
        Assert.DoesNotContain(
            actual,
            candidate =>
                candidate.OrderId
                == completedOverdue.OrderId);
    }

    [Fact]
    public async Task GetOverdueActiveHonorsBatchSizeAndDeadlineOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var oldest =
            CreateWaitingForInventorySaga(
                NowUtc.AddHours(-2),
                NowUtc.AddMinutes(-40));

        var newer =
            CreateWaitingForInventorySaga(
                NowUtc.AddHours(-1),
                NowUtc.AddMinutes(-20));

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(
                    dbContext);

            await repository.AddAsync(
                newer,
                cancellationToken);
            await repository.AddAsync(
                oldest,
                cancellationToken);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        await using var queryDbContext =
            fixture.CreateDbContext();

        var repository =
            new CheckoutSagaRepository(
                queryDbContext);

        var limited =
            await repository.GetOverdueActiveAsync(
                NowUtc,
                batchSize: 1,
                cancellationToken);

        Assert.Single(limited);

        var actual =
            await repository.GetOverdueActiveAsync(
                NowUtc,
                batchSize: 1000,
                cancellationToken);

        var orderIds =
            actual
                .Select(
                    saga => saga.OrderId)
                .ToArray();

        var oldestIndex =
            Array.IndexOf(
                orderIds,
                oldest.OrderId);
        var newerIndex =
            Array.IndexOf(
                orderIds,
                newer.OrderId);

        Assert.True(
            oldestIndex >= 0,
            "Oldest test Saga was not returned.");
        Assert.True(
            newerIndex >= 0,
            "Newer test Saga was not returned.");
        Assert.True(
            oldestIndex < newerIndex,
            "Overdue Sagas must be ordered by deadline.");
    }

    private static CheckoutSaga CreateWaitingForInventorySaga(
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        var saga =
            CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateItems(),
                "USD",
                35m,
                startedAtUtc,
                deadlineAtUtc);

        saga.BeginInventoryReservation(
            Guid.NewGuid(),
            startedAtUtc.AddSeconds(1),
            deadlineAtUtc);

        return saga;
    }

    private static CheckoutSaga CreateCompletedSaga(
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        var saga =
            CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateItems(),
                "USD",
                35m,
                startedAtUtc,
                deadlineAtUtc);

        var reservationId =
            Guid.NewGuid();
        var paymentId =
            Guid.NewGuid();

        saga.BeginInventoryReservation(
            reservationId,
            startedAtUtc.AddSeconds(1),
            deadlineAtUtc);
        saga.ConfirmInventoryReserved(
            reservationId,
            paymentId,
            startedAtUtc.AddSeconds(2));
        saga.ConfirmPaymentCaptured(
            paymentId,
            startedAtUtc.AddSeconds(3));
        saga.ConfirmInventoryConsumed(
            reservationId,
            startedAtUtc.AddSeconds(4));
        saga.CompleteSuccessfully(
            startedAtUtc.AddSeconds(5));

        return saga;
    }

    private static CheckoutSagaItem[] CreateItems()
    {
        return
        [
            CheckoutSagaItem.Create(
                "SKU-DEADLINE-001",
                2,
                10m,
                "USD"),
            CheckoutSagaItem.Create(
                "SKU-DEADLINE-002",
                3,
                5m,
                "USD")
        ];
    }
}
