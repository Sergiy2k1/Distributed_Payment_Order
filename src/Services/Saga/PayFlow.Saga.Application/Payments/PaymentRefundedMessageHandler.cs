using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Payments;

public sealed class PaymentRefundedMessageHandler
    : IPaymentRefundedMessageHandler
{
    private const string CompensationReason =
        "POST_CAPTURE_COMPENSATION";

    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PaymentRefundedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PaymentRefundedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "PaymentRefunded OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{payload.OrderId:D}' does not exist.");

        if (saga.PaymentId != payload.PaymentId
            || saga.RefundId != payload.RefundId)
        {
            throw new InvalidOperationException(
                "PaymentRefunded identities do not match persisted Saga compensation context.");
        }

        if (saga.TotalAmount != payload.Amount
            || !string.Equals(
                saga.Currency,
                payload.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PaymentRefunded amount or currency contradicts persisted Saga checkout snapshot.");
        }

        if (saga.ReservationId is not { } reservationId)
        {
            throw new InvalidOperationException(
                "Post-capture compensation requires persisted ReservationId.");
        }

        if (saga.Status is CheckoutSagaStatus.CompensatingInventory
            or CheckoutSagaStatus.CompensatingInventoryRestock)
        {
            return;
        }

        Guid? restockOperationId =
            saga.PostCaptureCompensationMode
                == PostCaptureCompensationMode.RestockConsumedInventory
                ? Guid.NewGuid()
                : null;

        saga.ConfirmPaymentRefunded(
            payload.PaymentId,
            payload.RefundId,
            restockOperationId,
            envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

        OutgoingIntegrationMessage outgoing =
            saga.PostCaptureCompensationMode switch
            {
                PostCaptureCompensationMode.ReleaseReservedInventory =>
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
                            CompensationReason)),

                PostCaptureCompensationMode.RestockConsumedInventory =>
                    new OutgoingIntegrationMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "RestockInventory.v1",
                            1,
                            saga.OrderId,
                            envelope.CorrelationId,
                            envelope.MessageId,
                            envelope.OccurredAtUtc,
                            "Saga",
                            envelope.TraceParent),
                        "inventory.commands",
                        new RestockInventoryV1(
                            saga.OrderId,
                            reservationId,
                            saga.RestockOperationId
                                ?? throw new InvalidOperationException(
                                    "Restock compensation is missing RestockOperationId."),
                            CompensationReason)),

                _ => throw new InvalidOperationException(
                    "Post-capture compensation mode is missing.")
            };

        await _outboxWriter.AddAsync(
            outgoing,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
