using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;

namespace PayFlow.Payment.Application.Capture;

public sealed class CapturePaymentMessageHandler
    : ICapturePaymentMessageHandler
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProviderOperationRepository _providerOperationRepository;
    private readonly IPaymentUnitOfWork _unitOfWork;

    public CapturePaymentMessageHandler(
        IPaymentRepository paymentRepository,
        IProviderOperationRepository providerOperationRepository,
        IPaymentUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _providerOperationRepository = providerOperationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        CapturePaymentMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = message.Envelope;
        var payload = message.Payload;

        if (payload.OrderId == Guid.Empty
            || payload.PaymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId and PaymentId must be non-empty.",
                nameof(message));
        }

        if (envelope.AggregateId != payload.OrderId)
        {
            throw new ArgumentException(
                "CapturePayment OrderId must match AggregateId.",
                nameof(message));
        }

        var existingPayment =
            await _paymentRepository.GetByIdAsync(
                payload.PaymentId,
                cancellationToken);

        if (existingPayment is not null)
        {
            EnsureEquivalent(existingPayment, payload);
            return;
        }

        var payment = Payment.Create(
            payload.PaymentId,
            payload.OrderId,
            payload.Amount,
            payload.Currency,
            envelope.OccurredAtUtc);

        payment.StartProcessing(
            envelope.OccurredAtUtc);

        var providerOperation =
            ProviderOperation.CreateCapture(
                Guid.NewGuid(),
                payload.PaymentId,
                envelope.OccurredAtUtc);

        await _paymentRepository.AddAsync(
            payment,
            cancellationToken);

        await _providerOperationRepository.AddAsync(
            providerOperation,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    private static void EnsureEquivalent(
        Payment payment,
        CapturePaymentV1 payload)
    {
        if (payment.OrderId != payload.OrderId
            || payment.Amount != payload.Amount
            || !string.Equals(
                payment.Currency,
                payload.Currency.Trim().ToUpperInvariant(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PaymentId was already used with a conflicting logical capture request.");
        }
    }
}
