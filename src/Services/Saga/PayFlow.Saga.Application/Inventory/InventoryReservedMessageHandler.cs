using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Payments;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Inventory;

public sealed class InventoryReservedMessageHandler
    : IInventoryReservedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public InventoryReservedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        InventoryReservedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = message.Payload;
        var envelope = message.Envelope;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "InventoryReserved OrderId must match AggregateId.",
                nameof(message));
        }

        var saga = await _repository
            .GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.Status == CheckoutSagaStatus.WaitingForPayment)
        {
            if (saga.ReservationId == payload.ReservationId
                && saga.PaymentId.HasValue)
            {
                return;
            }

            throw new InvalidOperationException(
                "InventoryReserved contradicts persisted Saga workflow identities.");
        }

        if (saga.ReservationExpiresAtUtc != payload.ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "InventoryReserved expiry does not match the persisted reservation.");
        }

        var paymentId = Guid.NewGuid();

        saga.ConfirmInventoryReserved(
            payload.ReservationId,
            paymentId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "CapturePayment.v1",
                    1,
                    saga.OrderId,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    envelope.OccurredAtUtc,
                    "Saga",
                    envelope.TraceParent),
                "payments.commands",
                new CapturePaymentV1(
                    saga.OrderId,
                    paymentId,
                    saga.TotalAmount,
                    saga.Currency)),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
