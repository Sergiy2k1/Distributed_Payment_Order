using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Inventory;

public sealed class InventoryReservationRejectedMessageHandler
    : IInventoryReservationRejectedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public InventoryReservationRejectedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        InventoryReservationRejectedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        var payload = message.Payload;
        var envelope = message.Envelope;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "InventoryReservationRejected OrderId must match AggregateId.",
                nameof(message));
        }

        var saga = await _repository
            .GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.Status == CheckoutSagaStatus.WaitingForOrderCancellation)
        {
            if (saga.ReservationId == payload.ReservationId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Inventory rejection references a different reservation.");
        }

        saga.RejectInventoryReservation(
            payload.ReservationId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "CancelOrder.v1",
                    1,
                    saga.OrderId,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    envelope.OccurredAtUtc,
                    "Saga",
                    envelope.TraceParent),
                "orders.commands",
                new CancelOrderV1(
                    saga.OrderId,
                    payload.ReasonCode)),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
