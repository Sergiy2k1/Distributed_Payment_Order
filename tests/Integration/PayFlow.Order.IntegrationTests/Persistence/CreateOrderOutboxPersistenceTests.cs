using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Entities;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.Infrastructure.Time;
using PayFlow.Order.IntegrationTests.Infrastructure;

namespace PayFlow.Order.IntegrationTests.Persistence;

public sealed class CreateOrderOutboxPersistenceTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task CreateOrderPersistsBusinessStateIdempotencyAndOutboxAtomically()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var customerId = Guid.NewGuid();
        const string idempotencyKey =
            "create-order-outbox-atomic";
        var command = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-OUTBOX",
                    2,
                    12.50m,
                    "USD")
            ]);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var handler = new CreateOrderHandler(
                new OrderRepository(dbContext),
                new CreateOrderIdempotencyRepository(
                    dbContext),
                new OrderOutboxWriter(dbContext),
                new EfUnitOfWork(dbContext),
                new SystemClock());

            await handler.HandleAsync(
                command,
                idempotencyKey,
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var order = await verificationDbContext.Orders
            .AsNoTracking()
            .SingleAsync(
                entity => entity.CustomerId == customerId,
                cancellationToken);

        var idempotencyRecord =
            await verificationDbContext
                .CreateOrderIdempotencyRecords
                .AsNoTracking()
                .SingleAsync(
                    record =>
                        record.IdempotencyKey == idempotencyKey,
                    cancellationToken);

        var outboxMessage =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == order.Id,
                    cancellationToken);

        Assert.Equal(
            order.Id,
            idempotencyRecord.OrderId);
        Assert.Equal(
            "OrderCreated.v1",
            outboxMessage.MessageType);
        Assert.Equal(
            1,
            outboxMessage.SchemaVersion);
        Assert.Equal(
            order.Id,
            outboxMessage.CorrelationId);
        Assert.Equal(
            "orders.events",
            outboxMessage.Destination);
        Assert.Null(outboxMessage.PublishedAtUtc);

        using var payload =
            JsonDocument.Parse(outboxMessage.Payload);

        Assert.Equal(
            order.Id,
            payload.RootElement
                .GetProperty("orderId")
                .GetGuid());
        Assert.Equal(
            customerId,
            payload.RootElement
                .GetProperty("customerId")
                .GetGuid());
    }

    [Fact]
    public async Task FailedOutboxInsertRollsBackOrderAndIdempotency()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var customerId = Guid.NewGuid();
        var outboxMessageId = Guid.NewGuid();
        const string idempotencyKey =
            "create-order-outbox-rollback";
        var command = new CreateOrderCommand(
            customerId,
            [
                new CreateOrderItem(
                    "SKU-OUTBOX-ROLLBACK",
                    1,
                    9.99m,
                    "USD")
            ]);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var handler = new CreateOrderHandler(
                new OrderRepository(dbContext),
                new CreateOrderIdempotencyRepository(
                    dbContext),
                new InvalidSchemaVersionOutboxWriter(
                    dbContext,
                    outboxMessageId),
                new EfUnitOfWork(dbContext),
                new SystemClock());

            await Assert.ThrowsAsync<DbUpdateException>(
                () => handler.HandleAsync(
                    command,
                    idempotencyKey,
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var orderCount = await verificationDbContext.Orders
            .AsNoTracking()
            .CountAsync(
                entity => entity.CustomerId == customerId,
                cancellationToken);

        var idempotencyCount =
            await verificationDbContext
                .CreateOrderIdempotencyRecords
                .AsNoTracking()
                .CountAsync(
                    record =>
                        record.IdempotencyKey == idempotencyKey,
                    cancellationToken);

        var outboxCount =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.OutboxMessageId == outboxMessageId,
                    cancellationToken);

        Assert.Equal(0, orderCount);
        Assert.Equal(0, idempotencyCount);
        Assert.Equal(0, outboxCount);
    }

    private sealed class InvalidSchemaVersionOutboxWriter(
        OrderDbContext dbContext,
        Guid outboxMessageId)
        : IOutboxWriter
    {
        public Task AddAsync(
            IDomainEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);

            var message = new OutboxMessageEntity
            {
                OutboxMessageId = outboxMessageId,
                MessageId = Guid.NewGuid(),
                MessageType = "OrderCreated.v1",
                SchemaVersion = 0,
                AggregateId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                CausationId = null,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Destination = "orders.events",
                Producer = "Order",
                TraceParent = null,
                Payload = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                PublishedAtUtc = null,
                AttemptCount = 0,
                NextAttemptAtUtc = null,
                LastErrorCode = null
            };

            return dbContext.OutboxMessages
                .AddAsync(
                    message,
                    cancellationToken)
                .AsTask();
        }
    }
}
