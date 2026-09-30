using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Payments;

public sealed class PaymentReconciledMessageHandler
    : IPaymentReconciledMessageHandler
{
    private const string InconsistentSnapshotCode =
        "PAYMENT_RECONCILIATION_INCONSISTENT";

    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PaymentReconciledMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PaymentReconciledMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId == Guid.Empty
            || payload.PaymentId == Guid.Empty
            || payload.ReconciliationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId, PaymentId and ReconciliationId must be non-empty.",
                nameof(message));
        }

        if (payload.OrderId != envelope.AggregateId)
        {
            throw new ArgumentException(
                "PaymentReconciled OrderId must match AggregateId.",
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
                "PaymentReconciled PaymentId does not match persisted Saga PaymentId.");
        }

        if (saga.Status != CheckoutSagaStatus.WaitingForPayment)
        {
            return;
        }

        switch (payload.PaymentStatus)
        {
            case "Captured":
                await HandleCapturedAsync(
                        saga,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case "Failed":
                await HandleFailedAsync(
                        saga,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case "Processing":
                if (IsRecoverableProcessingSnapshot(payload))
                {
                    return;
                }

                await RequireManualInterventionAsync(
                        saga,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            default:
                await RequireManualInterventionAsync(
                        saga,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleCapturedAsync(
        CheckoutSaga saga,
        PaymentReconciledMessage message,
        CancellationToken cancellationToken)
    {
        var payload = message.Payload;

        if (!string.Equals(
                payload.ProviderOperationStatus,
                "Succeeded",
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(
                payload.ProviderReference))
        {
            await RequireManualInterventionAsync(
                    saga,
                    message,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (saga.ReservationId is not { } reservationId)
        {
            throw new InvalidOperationException(
                "Captured payment reconciliation requires a persisted inventory reservation.");
        }

        saga.ConfirmPaymentCaptured(
            payload.PaymentId,
            message.Envelope.OccurredAtUtc);

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
                    message.Envelope.CorrelationId,
                    message.Envelope.MessageId,
                    message.Envelope.OccurredAtUtc,
                    "Saga",
                    message.Envelope.TraceParent),
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

    private async Task HandleFailedAsync(
        CheckoutSaga saga,
        PaymentReconciledMessage message,
        CancellationToken cancellationToken)
    {
        var payload = message.Payload;

        if (!string.Equals(
                payload.ProviderOperationStatus,
                "DefinitivelyFailed",
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(
                payload.LastErrorCode))
        {
            await RequireManualInterventionAsync(
                    saga,
                    message,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (saga.ReservationId is not { } reservationId)
        {
            throw new InvalidOperationException(
                "Failed payment reconciliation requires a persisted inventory reservation.");
        }

        saga.RejectPaymentCapture(
            payload.PaymentId,
            message.Envelope.OccurredAtUtc);

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
                    message.Envelope.CorrelationId,
                    message.Envelope.MessageId,
                    message.Envelope.OccurredAtUtc,
                    "Saga",
                    message.Envelope.TraceParent),
                "inventory.commands",
                new ReleaseInventoryV1(
                    saga.OrderId,
                    reservationId,
                    payload.LastErrorCode)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RequireManualInterventionAsync(
        CheckoutSaga saga,
        PaymentReconciledMessage message,
        CancellationToken cancellationToken)
    {
        var payload = message.Payload;
        var description =
            $"PaymentStatus={payload.PaymentStatus}; "
            + $"ProviderOperationStatus={payload.ProviderOperationStatus ?? "<null>"}; "
            + $"ProviderReferencePresent={!string.IsNullOrWhiteSpace(payload.ProviderReference)}; "
            + $"LastErrorCode={payload.LastErrorCode ?? "<null>"}.";

        saga.RequirePaymentReconciliationIntervention(
            InconsistentSnapshotCode,
            description,
            message.Envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
                saga,
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsRecoverableProcessingSnapshot(
        PaymentReconciledV1 payload)
    {
        if (payload.ProviderAttemptCount is < 0)
        {
            return false;
        }

        return payload.ProviderOperationStatus switch
        {
            "Pending" => true,
            "Processing" => true,
            "Ambiguous" =>
                payload.NextAttemptAtUtc is not null,
            _ => false
        };
    }
}
