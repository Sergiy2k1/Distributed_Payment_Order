using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class OrderProcessingStartedInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeadlineAtUtc =
        StartedAtUtc.AddMinutes(30);

    [Fact]
    public async Task ProcessingEventMovesSagaAndEnqueuesReserveInventoryAtomically()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await SeedStartedSagaAsync(
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(dbContext);

            var handler =
                new OrderProcessingStartedMessageHandler(
                    repository,
                    new SagaOutboxWriter(dbContext),
                    new SagaEfUnitOfWork(dbContext));

            var processor =
                new OrderProcessingStartedInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    handler);

            Assert.True(
                await processor.ProcessAsync(
                    CreateConsumedMessage(
                        orderId,
                        messageId,
                        correlationId),
                    StartedAtUtc.AddSeconds(2),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var saga =
            await verificationDbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.WaitingForInventory.ToString(),
            saga.Status);
        Assert.NotNull(saga.ReservationId);
        Assert.Equal(
            DeadlineAtUtc,
            saga.ReservationExpiresAtUtc);
        Assert.Equal(1, saga.Version);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                            == "ReserveInventory.v1",
                    cancellationToken);

        Assert.Equal(
            correlationId,
            outbox.CorrelationId);
        Assert.Equal(
            messageId,
            outbox.CausationId);
        Assert.Equal(
            "inventory.commands",
            outbox.Destination);

        var payload =
            JsonSerializer.Deserialize<ReserveInventoryV1>(
                outbox.Payload,
                SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal(
            saga.ReservationId,
            payload.ReservationId);
        Assert.Equal(
            DeadlineAtUtc,
            payload.ExpiresAtUtc);
        Assert.Equal(2, payload.Items.Count);
    }

    [Fact]
    public async Task SecondBusinessEquivalentEventDoesNotCreateSecondReservationCommand()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();

        await SeedStartedSagaAsync(
            orderId,
            cancellationToken);

        await ProcessAsync(
            orderId,
            Guid.NewGuid(),
            cancellationToken);

        await ProcessAsync(
            orderId,
            Guid.NewGuid(),
            cancellationToken);

        await using var verificationDbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            1,
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                            == "ReserveInventory.v1",
                    cancellationToken));
    }

    private async Task ProcessAsync(
        Guid orderId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var repository =
            new CheckoutSagaRepository(dbContext);

        var processor =
            new OrderProcessingStartedInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                new OrderProcessingStartedMessageHandler(
                    repository,
                    new SagaOutboxWriter(dbContext),
                    new SagaEfUnitOfWork(dbContext)));

        await processor.ProcessAsync(
            CreateConsumedMessage(
                orderId,
                messageId,
                orderId),
            StartedAtUtc.AddSeconds(2),
            cancellationToken);
    }

    private async Task SeedStartedSagaAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var saga =
            CheckoutSaga.Start(
                orderId,
                Guid.NewGuid(),
                [
                    CheckoutSagaItem.Create(
                        "SKU-001",
                        1,
                        15m,
                        "USD"),
                    CheckoutSagaItem.Create(
                        "SKU-002",
                        2,
                        10m,
                        "USD")
                ],
                "USD",
                35m,
                StartedAtUtc,
                DeadlineAtUtc);

        await using var dbContext =
            fixture.CreateDbContext();

        await new CheckoutSagaRepository(dbContext)
            .AddAsync(
                saga,
                cancellationToken);

        await dbContext
            .SaveChangesAsync(cancellationToken);
    }

    private static ConsumedOrderProcessingStartedMessage
        CreateConsumedMessage(
            Guid orderId,
            Guid messageId,
            Guid correlationId)
    {
        return new ConsumedOrderProcessingStartedMessage(
            new OrderProcessingStartedMessage(
                new IntegrationMessageEnvelope(
                    messageId,
                    "OrderProcessingStarted.v1",
                    1,
                    orderId,
                    correlationId,
                    null,
                    StartedAtUtc.AddSeconds(1),
                    "Order",
                    null),
                new OrderProcessingStartedV1(
                    orderId)),
            "orders.events",
            0,
            400,
            StartedAtUtc.AddSeconds(1));
    }
}
