using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Application.Payments;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Messaging;

public sealed class PostCaptureCompensationFlowTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 9, 29, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefundedCapturedOrderRestocksInventoryCancelsOrderAndCompletesSaga()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        await SeedWaitingForOrderConfirmationSagaAsync(
            orderId,
            reservationId,
            paymentId,
            cancellationToken);

        await StartCompensationAsync(
            orderId,
            refundId,
            cancellationToken);

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var saga =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        entity => entity.OrderId == orderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.CompensatingPayment.ToString(),
                saga.Status);
            Assert.Equal(
                refundId,
                saga.RefundId);
            Assert.Equal(
                PostCaptureCompensationMode.RestockConsumedInventory.ToString(),
                saga.PostCaptureCompensationMode);

            var refundCommand =
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(
                        message =>
                            message.AggregateId == orderId
                            && message.MessageType == "RefundPayment.v1",
                        cancellationToken);

            Assert.Equal(
                "payments.commands",
                refundCommand.Destination);

            var refundPayload =
                JsonSerializer.Deserialize<RefundPaymentV1>(
                    refundCommand.Payload,
                    SerializerOptions);

            Assert.NotNull(refundPayload);
            Assert.Equal(orderId, refundPayload.OrderId);
            Assert.Equal(paymentId, refundPayload.PaymentId);
            Assert.Equal(refundId, refundPayload.RefundId);
        }

        await ProcessPaymentRefundedAsync(
            orderId,
            paymentId,
            refundId,
            cancellationToken);

        Guid restockOperationId;

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var saga =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        entity => entity.OrderId == orderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.CompensatingInventoryRestock.ToString(),
                saga.Status);
            Assert.NotNull(
                saga.RestockOperationId);

            restockOperationId =
                saga.RestockOperationId.Value;

            var restockCommand =
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(
                        message =>
                            message.AggregateId == orderId
                            && message.MessageType == "RestockInventory.v1",
                        cancellationToken);

            Assert.Equal(
                "inventory.commands",
                restockCommand.Destination);

            var restockPayload =
                JsonSerializer.Deserialize<RestockInventoryV1>(
                    restockCommand.Payload,
                    SerializerOptions);

            Assert.NotNull(restockPayload);
            Assert.Equal(orderId, restockPayload.OrderId);
            Assert.Equal(reservationId, restockPayload.ReservationId);
            Assert.Equal(
                restockOperationId,
                restockPayload.RestockOperationId);
            Assert.Equal(
                "POST_CAPTURE_COMPENSATION",
                restockPayload.ReasonCode);
        }

        await ProcessInventoryRestockedAsync(
            orderId,
            reservationId,
            restockOperationId,
            cancellationToken);

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var saga =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        entity => entity.OrderId == orderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.WaitingForOrderCancellation.ToString(),
                saga.Status);

            var cancelCommand =
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(
                        message =>
                            message.AggregateId == orderId
                            && message.MessageType == "CancelOrder.v1",
                        cancellationToken);

            Assert.Equal(
                "orders.commands",
                cancelCommand.Destination);

            var cancelPayload =
                JsonSerializer.Deserialize<CancelOrderV1>(
                    cancelCommand.Payload,
                    SerializerOptions);

            Assert.NotNull(cancelPayload);
            Assert.Equal(orderId, cancelPayload.OrderId);
            Assert.Equal(
                "POST_CAPTURE_COMPENSATION",
                cancelPayload.ReasonCode);
        }

        await ProcessOrderCancelledAsync(
            orderId,
            cancellationToken);

        await using var finalDbContext =
            fixture.CreateDbContext();

        var completedSaga =
            await finalDbContext.CheckoutSagas
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.OrderId == orderId,
                    cancellationToken);

        Assert.Equal(
            CheckoutSagaStatus.CompletedWithBusinessFailure.ToString(),
            completedSaga.Status);

        Assert.Equal(
            3,
            await finalDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId == orderId,
                    cancellationToken));
    }

    private async Task SeedWaitingForOrderConfirmationSagaAsync(
        Guid orderId,
        Guid reservationId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var deadlineAtUtc =
            StartedAtUtc.AddMinutes(30);

        var saga =
            CheckoutSaga.Start(
                orderId,
                Guid.NewGuid(),
                [
                    CheckoutSagaItem.Create(
                        "SKU-POST-CAPTURE",
                        2,
                        17.5m,
                        "USD")
                ],
                "USD",
                35m,
                StartedAtUtc,
                deadlineAtUtc);

        saga.BeginInventoryReservation(
            reservationId,
            StartedAtUtc.AddSeconds(1),
            deadlineAtUtc);
        saga.ConfirmInventoryReserved(
            reservationId,
            paymentId,
            StartedAtUtc.AddSeconds(2));
        saga.ConfirmPaymentCaptured(
            paymentId,
            StartedAtUtc.AddSeconds(3));
        saga.ConfirmInventoryConsumed(
            reservationId,
            StartedAtUtc.AddSeconds(4));

        await using var dbContext =
            fixture.CreateDbContext();

        await new CheckoutSagaRepository(dbContext)
            .AddAsync(
                saga,
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task StartCompensationAsync(
        Guid orderId,
        Guid refundId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var starter =
            new PostCaptureCompensationStarter(
                new CheckoutSagaRepository(
                    dbContext),
                new SagaOutboxWriter(
                    dbContext),
                new SagaEfUnitOfWork(
                    dbContext));

        await starter.StartAsync(
            new PostCaptureCompensationRequest(
                orderId,
                refundId,
                orderId,
                Guid.NewGuid(),
                StartedAtUtc.AddSeconds(5),
                "ORDER_CONFIRMATION_FAILED",
                null),
            cancellationToken);
    }

    private async Task ProcessPaymentRefundedAsync(
        Guid orderId,
        Guid paymentId,
        Guid refundId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var occurredAtUtc =
            StartedAtUtc.AddSeconds(6);

        var processor =
            new PaymentRefundedInboxProcessor(
                dbContext,
                new InboxMessageRepository(
                    dbContext),
                new PaymentRefundedMessageHandler(
                    new CheckoutSagaRepository(
                        dbContext),
                    new SagaOutboxWriter(
                        dbContext),
                    new SagaEfUnitOfWork(
                        dbContext)));

        Assert.True(
            await processor.ProcessAsync(
                new ConsumedPaymentRefundedMessage(
                    new PaymentRefundedMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "PaymentRefunded.v1",
                            1,
                            orderId,
                            orderId,
                            null,
                            occurredAtUtc,
                            "Payment",
                            null),
                        new PaymentRefundedV1(
                            orderId,
                            paymentId,
                            refundId,
                            35m,
                            "USD",
                            occurredAtUtc)),
                    "payments.events",
                    0,
                    1200,
                    occurredAtUtc),
                occurredAtUtc.AddMilliseconds(100),
                cancellationToken));
    }

    private async Task ProcessInventoryRestockedAsync(
        Guid orderId,
        Guid reservationId,
        Guid restockOperationId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var occurredAtUtc =
            StartedAtUtc.AddSeconds(7);

        var processor =
            new InventoryRestockedInboxProcessor(
                dbContext,
                new InboxMessageRepository(
                    dbContext),
                new InventoryRestockedMessageHandler(
                    new CheckoutSagaRepository(
                        dbContext),
                    new SagaOutboxWriter(
                        dbContext),
                    new SagaEfUnitOfWork(
                        dbContext)));

        Assert.True(
            await processor.ProcessAsync(
                new ConsumedInventoryRestockedMessage(
                    new InventoryRestockedMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "InventoryRestocked.v1",
                            1,
                            orderId,
                            orderId,
                            null,
                            occurredAtUtc,
                            "Inventory",
                            null),
                        new InventoryRestockedV1(
                            orderId,
                            reservationId,
                            restockOperationId,
                            occurredAtUtc)),
                    "inventory.events",
                    0,
                    1201,
                    occurredAtUtc),
                occurredAtUtc.AddMilliseconds(100),
                cancellationToken));
    }

    private async Task ProcessOrderCancelledAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var occurredAtUtc =
            StartedAtUtc.AddSeconds(8);

        var processor =
            new OrderCancelledInboxProcessor(
                dbContext,
                new InboxMessageRepository(
                    dbContext),
                new OrderCancelledMessageHandler(
                    new CheckoutSagaRepository(
                        dbContext),
                    new SagaEfUnitOfWork(
                        dbContext)));

        Assert.True(
            await processor.ProcessAsync(
                new ConsumedOrderCancelledMessage(
                    new OrderCancelledMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "OrderCancelled.v1",
                            1,
                            orderId,
                            orderId,
                            null,
                            occurredAtUtc,
                            "Order",
                            null),
                        new OrderCancelledV1(
                            orderId,
                            "POST_CAPTURE_COMPENSATION",
                            occurredAtUtc)),
                    "orders.events",
                    0,
                    1202,
                    occurredAtUtc),
                occurredAtUtc.AddMilliseconds(100),
                cancellationToken));
    }
}
