using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;

namespace PayFlow.Saga.Application.Inventory;

public sealed class InventoryRestockedMessageHandler
    : IInventoryRestockedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public InventoryRestockedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        InventoryRestockedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = message.Payload;
        var envelope = message.Envelope;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "InventoryRestocked OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.Status
            == global::PayFlow.Saga.Domain.Checkout.CheckoutSagaStatus.WaitingForOrderCancellation)
        {
            return;
        }

        saga.ConfirmInventoryRestocked(
            payload.ReservationId,
            payload.RestockOperationId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

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
                    "POST_CAPTURE_COMPENSATION")),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
