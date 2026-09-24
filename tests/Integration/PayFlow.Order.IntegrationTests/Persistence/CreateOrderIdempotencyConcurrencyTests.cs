using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.Infrastructure.Time;
using PayFlow.Order.IntegrationTests.Infrastructure;

namespace PayFlow.Order.IntegrationTests.Persistence;

public sealed class CreateOrderIdempotencyConcurrencyTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ConcurrentSameKeyAndPayloadCreateOnlyOneOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var gate = new ConcurrentMissingLookupGate();
        var customerId = Guid.NewGuid();
        var command = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-RACE",
                    2,
                    12.50m,
                    "USD")
            ]);
        const string idempotencyKey =
            "create-order-concurrent-same-payload";

        await using var firstDbContext =
            fixture.CreateDbContext();
        await using var secondDbContext =
            fixture.CreateDbContext();

        var firstHandler = CreateHandler(
            firstDbContext,
            gate);
        var secondHandler = CreateHandler(
            secondDbContext,
            gate);

        var firstTask = firstHandler.HandleAsync(
            command,
            idempotencyKey,
            cancellationToken);
        var secondTask = secondHandler.HandleAsync(
            command,
            idempotencyKey,
            cancellationToken);

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.Equal(
            results[0],
            results[1]);

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var orderCount = await verificationDbContext.Orders
            .AsNoTracking()
            .CountAsync(
                order => order.CustomerId == customerId,
                cancellationToken);

        var idempotencyCount =
            await verificationDbContext
                .CreateOrderIdempotencyRecords
                .AsNoTracking()
                .CountAsync(
                    record =>
                        record.IdempotencyKey == idempotencyKey,
                    cancellationToken);

        Assert.Equal(1, orderCount);
        Assert.Equal(1, idempotencyCount);
    }

    private static CreateOrderHandler CreateHandler(
        OrderDbContext dbContext,
        ConcurrentMissingLookupGate gate)
    {
        var innerIdempotencyRepository =
            new CreateOrderIdempotencyRepository(
                dbContext);

        return new CreateOrderHandler(
            new OrderRepository(dbContext),
            new GatedIdempotencyRepository(
                innerIdempotencyRepository,
                gate),
            new EfUnitOfWork(dbContext),
            new SystemClock());
    }

    private sealed class GatedIdempotencyRepository(
        ICreateOrderIdempotencyRepository inner,
        ConcurrentMissingLookupGate gate)
        : ICreateOrderIdempotencyRepository
    {
        public async Task<CreateOrderIdempotencyRecord?> GetByKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.GetByKeyAsync(
                idempotencyKey,
                cancellationToken);

            if (result is null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            return result;
        }

        public Task AddAsync(
            CreateOrderIdempotencyRecord record,
            CancellationToken cancellationToken = default)
        {
            return inner.AddAsync(
                record,
                cancellationToken);
        }
    }

    private sealed class ConcurrentMissingLookupGate
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _participantCount;

        public Task WaitAsync(
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(
                    ref _participantCount) == 2)
            {
                _release.TrySetResult();
            }

            return _release.Task.WaitAsync(
                cancellationToken);
        }
    }
}
