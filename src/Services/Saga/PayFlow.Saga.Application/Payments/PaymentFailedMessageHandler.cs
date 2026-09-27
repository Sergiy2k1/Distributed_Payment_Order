using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Payments;

public sealed class PaymentFailedMessageHandler
    : IPaymentFailedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PaymentFailedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PaymentFailedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "PaymentFailed OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.PaymentId != payload.PaymentId)
        {
            throw new InvalidOperationException(
                "PaymentFailed PaymentId does not match persisted Saga PaymentId.");
        }

        if (saga.Status == CheckoutSagaStatus.CompensatingInventory)
        {
            return;
        }

        if (saga.ReservationId is not { } reservationId)
        {
            throw new InvalidOperationException(
                "Checkout Saga has no inventory reservation to release.");
        }

        saga.RejectPaymentCapture(
            payload.PaymentId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "ReleaseInventory.v1",
                    1,
                    saga.OrderId,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    envelope.OccurredAtUtc,
                    "Saga",
                    envelope.TraceParent),
                "inventory.commands",
                new ReleaseInventoryV1(
                    saga.OrderId,
                    reservationId,
                    payload.ReasonCode)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
