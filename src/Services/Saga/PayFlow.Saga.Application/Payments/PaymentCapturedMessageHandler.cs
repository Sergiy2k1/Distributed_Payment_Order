using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Payments;

public sealed class PaymentCapturedMessageHandler
    : IPaymentCapturedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PaymentCapturedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PaymentCapturedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "PaymentCaptured OrderId must match AggregateId.",
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
                "PaymentCaptured PaymentId does not match persisted Saga PaymentId.");
        }

        if (saga.TotalAmount != payload.Amount
            || !string.Equals(
                saga.Currency,
                payload.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PaymentCaptured amount or currency contradicts persisted Saga checkout snapshot.");
        }

        if (saga.Status == CheckoutSagaStatus.WaitingForInventoryCommit)
        {
            return;
        }

        if (saga.ReservationId is not { } reservationId)
        {
            throw new InvalidOperationException(
                "Checkout Saga has no inventory reservation to consume.");
        }

        saga.ConfirmPaymentCaptured(
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
                    "ConsumeInventory.v1",
                    1,
                    saga.OrderId,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    envelope.OccurredAtUtc,
                    "Saga",
                    envelope.TraceParent),
                "inventory.commands",
                new ConsumeInventoryV1(
                    saga.OrderId,
                    reservationId)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
