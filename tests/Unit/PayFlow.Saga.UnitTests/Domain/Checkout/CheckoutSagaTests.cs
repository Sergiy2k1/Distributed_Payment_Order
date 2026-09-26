using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.UnitTests.Domain.Checkout;

public sealed class CheckoutSagaTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 26, 11, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public void StartCreatesPersistableStartedState()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var saga = CheckoutSaga.Start(
            orderId,
            customerId,
            CreateItems(),
            "usd",
            35m,
            StartedAtUtc,
            DeadlineAtUtc);

        Assert.Equal(orderId, saga.OrderId);
        Assert.Equal(customerId, saga.CustomerId);
        Assert.Equal(
            CheckoutSagaStatus.Started,
            saga.Status);
        Assert.Equal("USD", saga.Currency);
        Assert.Equal(35m, saga.TotalAmount);
        Assert.Equal(0, saga.Version);
        Assert.Equal(0, saga.RetryCount);
        Assert.Null(saga.NextAttemptAtUtc);
        Assert.Null(saga.ReservationId);
        Assert.Null(saga.ReservationExpiresAtUtc);
        Assert.Equal(
            DeadlineAtUtc,
            saga.DeadlineAtUtc);
        Assert.Equal(2, saga.Items.Count);
    }

    [Fact]
    public void StartRejectsTotalThatDiffersFromItemSnapshot()
    {
        Assert.Throws<ArgumentException>(
            () => CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateItems(),
                "USD",
                999m,
                StartedAtUtc,
                DeadlineAtUtc));
    }

    [Fact]
    public void StartRejectsDeadlineNotAfterStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateItems(),
                "USD",
                35m,
                StartedAtUtc,
                StartedAtUtc));
    }

    [Fact]
    public void StartRejectsMixedCurrencies()
    {
        var items = new[]
        {
            CheckoutSagaItem.Create(
                "SKU-001",
                2,
                10m,
                "USD"),
            CheckoutSagaItem.Create(
                "SKU-002",
                3,
                5m,
                "EUR")
        };

        Assert.Throws<ArgumentException>(
            () => CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                items,
                "USD",
                35m,
                StartedAtUtc,
                DeadlineAtUtc));
    }


    [Fact]
    public void BeginInventoryReservationMovesSagaToWaitingForInventory()
    {
        var saga = CheckoutSaga.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateItems(),
            "USD",
            35m,
            StartedAtUtc,
            DeadlineAtUtc);
        var reservationId = Guid.NewGuid();
        var occurredAtUtc = StartedAtUtc.AddSeconds(5);

        saga.BeginInventoryReservation(
            reservationId,
            occurredAtUtc,
            DeadlineAtUtc);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForInventory,
            saga.Status);
        Assert.Equal(
            reservationId,
            saga.ReservationId);
        Assert.Equal(
            DeadlineAtUtc,
            saga.ReservationExpiresAtUtc);
        Assert.Equal(
            occurredAtUtc,
            saga.UpdatedAtUtc);
        Assert.Equal(1, saga.Version);
    }

    [Fact]
    public void BeginInventoryReservationReplayWithSameIdentityIsNoOp()
    {
        var saga = CheckoutSaga.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateItems(),
            "USD",
            35m,
            StartedAtUtc,
            DeadlineAtUtc);
        var reservationId = Guid.NewGuid();
        var occurredAtUtc = StartedAtUtc.AddSeconds(5);

        saga.BeginInventoryReservation(
            reservationId,
            occurredAtUtc,
            DeadlineAtUtc);

        saga.BeginInventoryReservation(
            reservationId,
            occurredAtUtc,
            DeadlineAtUtc);

        Assert.Equal(1, saga.Version);
    }

    private static CheckoutSagaItem[] CreateItems()
    {
        return
        [
            CheckoutSagaItem.Create(
                "SKU-001",
                2,
                10m,
                "USD"),
            CheckoutSagaItem.Create(
                "SKU-002",
                3,
                5m,
                "USD")
        ];
    }
}
