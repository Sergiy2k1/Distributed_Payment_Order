using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Messaging;
using PayFlow.Order.Application.Orders.CancelOrder;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Messaging;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.IntegrationTests.Infrastructure;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.IntegrationTests.Messaging;

public sealed class CancelOrderInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CancelCommandCancelsOrderAndPublishesOrderCancelled()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var occurredAtUtc = CreatedAtUtc.AddSeconds(2);

        await SeedProcessingOrderAsync(orderId, cancellationToken);

        await using (var dbContext = fixture.CreateDbContext())
        {
            var repository = new OrderRepository(dbContext);

            var processor = new CancelOrderInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                new CancelOrderMessageHandler(
                    repository,
                    new CancelOrderOutboxWriter(dbContext),
                    new EfUnitOfWork(dbContext)));

            Assert.True(
                await processor.ProcessAsync(
                    new ConsumedCancelOrderMessage(
                        new CancelOrderMessage(
                            new IntegrationMessageEnvelope(
                                messageId,
                                "CancelOrder.v1",
                                1,
                                orderId,
                                orderId,
                                null,
                                occurredAtUtc,
                                "Saga",
                                null),
                            new CancelOrderV1(
                                orderId,
                                "PAYMENT_FAILED")),
                        "orders.commands",
                        0,
                        300,
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
            OrderStatus.Cancelled.ToString(),
            order.Status);

        var outbox =
            await verificationDbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.AggregateId == orderId
                        && message.MessageType == "OrderCancelled.v1",
                    cancellationToken);

        Assert.Equal(messageId, outbox.CausationId);
        Assert.Contains("PAYMENT_FAILED", outbox.Payload);
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
                        Sku.From("SKU-CANCEL"),
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
            .AddAsync(order, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
