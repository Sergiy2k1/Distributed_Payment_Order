using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Inventory;

public sealed class InventoryConsumedMessageHandler
    : IInventoryConsumedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public InventoryConsumedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        InventoryConsumedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "InventoryConsumed OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.ReservationId != payload.ReservationId)
        {
            throw new InvalidOperationException(
                "InventoryConsumed ReservationId does not match persisted Saga reservation.");
        }

        if (saga.Status == CheckoutSagaStatus.WaitingForOrderConfirmation)
        {
            return;
        }

        saga.ConfirmInventoryConsumed(
            payload.ReservationId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "ConfirmOrder.v1",
                    1,
                    saga.OrderId,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    envelope.OccurredAtUtc,
                    "Saga",
                    envelope.TraceParent),
                "orders.commands",
                new ConfirmOrderV1(
                    saga.OrderId)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
