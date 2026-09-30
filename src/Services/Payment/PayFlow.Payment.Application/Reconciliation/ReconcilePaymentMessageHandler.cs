using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Events;

namespace PayFlow.Payment.Application.Reconciliation;

public sealed class ReconcilePaymentMessageHandler
    : IReconcilePaymentMessageHandler
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProviderOperationRepository _providerOperationRepository;
    private readonly IPaymentOutboxWriter _outboxWriter;
    private readonly IPaymentUnitOfWork _unitOfWork;

    public ReconcilePaymentMessageHandler(
        IPaymentRepository paymentRepository,
        IProviderOperationRepository providerOperationRepository,
        IPaymentOutboxWriter outboxWriter,
        IPaymentUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _providerOperationRepository = providerOperationRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        ReconcilePaymentMessage message,
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
                "ReconcilePayment OrderId must match AggregateId.",
                nameof(message));
        }

        var payment =
            await _paymentRepository.GetByIdAsync(
                payload.PaymentId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Payment '{payload.PaymentId:D}' does not exist.");

        if (payment.OrderId != payload.OrderId)
        {
            throw new InvalidOperationException(
                "ReconcilePayment OrderId does not match persisted Payment.");
        }

        var operation =
            await _providerOperationRepository
                .GetByBusinessOperationAsync(
                    "Capture",
                    payload.PaymentId,
                    cancellationToken)
                .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            payment.OrderId,
            envelope.CorrelationId,
            envelope.MessageId,
            envelope.OccurredAtUtc,
            "PaymentReconciled.v1",
            new PaymentReconciledV1(
                payment.OrderId,
                payment.PaymentId,
                payload.ReconciliationId,
                payment.Status.ToString(),
                operation?.Status.ToString(),
                operation?.AttemptCount,
                operation?.NextAttemptAtUtc,
                operation?.LastErrorCode,
                operation?.ProviderReference,
                envelope.OccurredAtUtc),
            envelope.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }
}
