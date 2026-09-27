using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;

namespace PayFlow.Payment.Application.Refund;

public sealed class RefundPaymentMessageHandler
    : IRefundPaymentMessageHandler
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProviderOperationRepository _providerOperationRepository;
    private readonly IPaymentUnitOfWork _unitOfWork;

    public RefundPaymentMessageHandler(
        IPaymentRepository paymentRepository,
        IProviderOperationRepository providerOperationRepository,
        IPaymentUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _providerOperationRepository = providerOperationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        RefundPaymentMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId == Guid.Empty
            || payload.PaymentId == Guid.Empty
            || payload.RefundId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId, PaymentId, and RefundId must be non-empty.",
                nameof(message));
        }

        if (payload.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(message),
                "Refund amount must be greater than zero.");
        }

        if (envelope.AggregateId != payload.OrderId)
        {
            throw new ArgumentException(
                "RefundPayment OrderId must match AggregateId.",
                nameof(message));
        }

        var payment =
            await _paymentRepository.GetByIdAsync(
                payload.PaymentId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Payment '{payload.PaymentId:D}' does not exist.");

        EnsureEquivalent(payment, payload);

        var existingOperation =
            await _providerOperationRepository
                .GetByBusinessOperationAsync(
                    "Refund",
                    payment.PaymentId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (existingOperation is not null)
        {
            if (existingOperation.ProviderOperationId != payload.RefundId)
            {
                throw new InvalidOperationException(
                    "Payment already has a different logical full refund operation.");
            }

            if (payment.Status is
                PaymentStatus.RefundPending
                or PaymentStatus.Refunded)
            {
                return;
            }

            throw new InvalidOperationException(
                "Refund provider operation exists but Payment state is inconsistent.");
        }

        if (payment.Status != PaymentStatus.Captured)
        {
            throw new InvalidOperationException(
                $"RefundPayment requires Captured Payment, but current state is '{payment.Status}'.");
        }

        payment.StartRefund(
            envelope.OccurredAtUtc);

        var providerOperation =
            ProviderOperation.CreateRefund(
                payload.RefundId,
                payment.PaymentId,
                envelope.OccurredAtUtc,
                envelope.CorrelationId,
                envelope.MessageId,
                envelope.TraceParent);

        await _paymentRepository.ApplyAsync(
            payment,
            cancellationToken)
            .ConfigureAwait(false);

        await _providerOperationRepository.AddAsync(
            providerOperation,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static void EnsureEquivalent(
        PayFlow.Payment.Domain.Payments.Payment payment,
        RefundPaymentV1 payload)
    {
        var normalizedCurrency =
            payload.Currency.Trim().ToUpperInvariant();

        if (payment.OrderId != payload.OrderId
            || payment.Amount != payload.Amount
            || !string.Equals(
                payment.Currency,
                normalizedCurrency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refund request contradicts the persisted captured Payment.");
        }
    }
}
