using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Messaging;
using PayFlow.Order.Application.Orders.ConfirmOrder;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Messaging;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.IntegrationTests.Infrastructure;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.IntegrationTests.Messaging;

public sealed class ConfirmOrderInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConfirmCommandTransitionsOrderAndPublishesOrderConfirmed()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc =
            CreatedAtUtc.AddSeconds(2);

        await SeedProcessingOrderAsync(
            orderId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new OrderRepository(dbContext);

            var processor =
                new ConfirmOrderInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new ConfirmOrderMessageHandler(
                        repository,
                        new OrderCommandOutboxWriter(dbContext),
                        new EfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedConfirmOrderMessage(
                        new ConfirmOrderMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "ConfirmOrder.v1",
                                1,
                                orderId,
                                correlationId,
                                null,
                                occurredAtUtc,
                                "Saga",
                                null),
                            new ConfirmOrderV1(orderId)),
                        "orders.commands",
                        0,
                        200,
                        occurredAtUtc),
                    occurredAtUtc.AddSeconds(1),
                    cancellationToken));
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var order =
            await verificationDbContext.Orders
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.Id == orderId,
                    cancellationToken);

        Assert.Equal(
            OrderStatus.Confirmed.ToString(),
            order.Status);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType
                        == "OrderConfirmed.v1",
                    cancellationToken);

        Assert.Equal(correlationId, outbox.CorrelationId);
        Assert.Equal(messageId, outbox.CausationId);

        using var payload =
            JsonDocument.Parse(outbox.Payload);

        Assert.Equal(
            orderId,
            payload.RootElement
                .GetProperty("orderId")
                .GetGuid());

        Assert.Equal(
            occurredAtUtc,
            payload.RootElement
                .GetProperty("confirmedAtUtc")
                .GetDateTimeOffset());
    }

    private async Task SeedProcessingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order =
            OrderAggregate.Create(
                OrderId.From(orderId),
                CustomerId.New(),
                [
                    OrderItem.Create(
                        Sku.From("SKU-CONFIRM"),
                        1,
                        Money.From(25m, "USD"))
                ],
                CreatedAtUtc);

        order.ClearDomainEvents();
        order.StartProcessing(
            CreatedAtUtc.AddSeconds(1));
        order.ClearDomainEvents();

        await using var dbContext =
            fixture.CreateDbContext();

        await new OrderRepository(dbContext)
            .AddAsync(
                order,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
