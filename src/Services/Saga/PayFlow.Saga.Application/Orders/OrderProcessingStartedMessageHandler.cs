using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Orders;

public sealed class OrderProcessingStartedMessageHandler
    : IOrderProcessingStartedMessageHandler
{
    private readonly ICheckoutSagaRepository _sagaRepository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public OrderProcessingStartedMessageHandler(
        ICheckoutSagaRepository sagaRepository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _sagaRepository = sagaRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        OrderProcessingStartedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Envelope);
        ArgumentNullException.ThrowIfNull(message.Payload);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "OrderProcessingStarted payload OrderId must match the envelope AggregateId.",
                nameof(message));
        }

        var saga = await _sagaRepository
            .GetByOrderIdAsync(
                message.Payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{message.Payload.OrderId:D}' does not exist.");

        if (saga.Status == CheckoutSagaStatus.WaitingForInventory)
        {
            return;
        }

        if (saga.Status != CheckoutSagaStatus.Started)
        {
            throw new InvalidOperationException(
                $"OrderProcessingStarted contradicts Checkout Saga state '{saga.Status}'.");
        }

        var reservationId = Guid.NewGuid();

        saga.BeginInventoryReservation(
            reservationId,
            message.Envelope.OccurredAtUtc,
            saga.DeadlineAtUtc);

        await _sagaRepository
            .ApplyAsync(
                saga,
                cancellationToken)
            .ConfigureAwait(false);

        var commandEnvelope =
            new IntegrationMessageEnvelope(
                Guid.NewGuid(),
                "ReserveInventory.v1",
                1,
                saga.OrderId,
                message.Envelope.CorrelationId,
                message.Envelope.MessageId,
                message.Envelope.OccurredAtUtc,
                "Saga",
                message.Envelope.TraceParent);

        await _outboxWriter
            .AddAsync(
                new OutgoingIntegrationMessage(
                    commandEnvelope,
                    "inventory.commands",
                    new ReserveInventoryV1(
                        saga.OrderId,
                        reservationId,
                        saga.Items
                            .Select(
                                static item =>
                                    new ReserveInventoryItemV1(
                                        item.SkuId,
                                        item.Quantity))
                            .ToArray(),
                        saga.DeadlineAtUtc)),
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
