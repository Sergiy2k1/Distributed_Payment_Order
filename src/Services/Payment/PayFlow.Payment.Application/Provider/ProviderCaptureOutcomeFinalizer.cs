using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Events;
using PayFlow.Payment.Domain.Ledger;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.Application.Provider;

public sealed class ProviderCaptureOutcomeFinalizer
    : IProviderCaptureOutcomeFinalizer
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProviderOperationRepository _providerOperationRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IPaymentOutboxWriter _outboxWriter;
    private readonly IPaymentUnitOfWork _unitOfWork;

    public ProviderCaptureOutcomeFinalizer(
        IPaymentRepository paymentRepository,
        IProviderOperationRepository providerOperationRepository,
        ILedgerRepository ledgerRepository,
        IPaymentOutboxWriter outboxWriter,
        IPaymentUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _providerOperationRepository = providerOperationRepository;
        _ledgerRepository = ledgerRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task FinalizeAsync(
        ProviderCaptureCompletionContext context,
        PaymentProviderCaptureResult result,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        var payment =
            await _paymentRepository.GetByIdAsync(
                context.PaymentId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Payment '{context.PaymentId:D}' does not exist.");

        if (payment.OrderId != context.OrderId)
        {
            throw new InvalidOperationException(
                "Provider completion OrderId does not match persisted Payment.");
        }

        var operation =
            await _providerOperationRepository
                .GetByBusinessOperationAsync(
                    "Capture",
                    context.PaymentId,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Capture operation for Payment '{context.PaymentId:D}' does not exist.");

        if (IsAlreadyApplied(
                payment,
                operation,
                result))
        {
            return;
        }

        switch (result.Outcome)
        {
            case PaymentProviderCaptureOutcome.Succeeded:
                await ApplySuccessAsync(
                    context,
                    result,
                    payment,
                    operation,
                    occurredAtUtc,
                    cancellationToken)
                    .ConfigureAwait(false);
                break;

            case PaymentProviderCaptureOutcome.DefinitivelyFailed:
                await ApplyFailureAsync(
                    context,
                    result,
                    payment,
                    operation,
                    occurredAtUtc,
                    cancellationToken)
                    .ConfigureAwait(false);
                break;

            case PaymentProviderCaptureOutcome.Ambiguous:
                ApplyAmbiguous(
                    result,
                    operation,
                    occurredAtUtc,
                    nextAttemptAtUtc);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported provider capture outcome '{result.Outcome}'.");
        }

        await _providerOperationRepository.ApplyAsync(
            operation,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsAlreadyApplied(
        PaymentAggregate payment,
        ProviderOperation operation,
        PaymentProviderCaptureResult result)
    {
        return result.Outcome switch
        {
            PaymentProviderCaptureOutcome.Succeeded
                when operation.Status
                    == ProviderOperationStatus.Succeeded
                && payment.Status
                    == PaymentStatus.Captured
                && string.Equals(
                    operation.ProviderReference,
                    result.ProviderReference,
                    StringComparison.Ordinal) =>
                true,

            PaymentProviderCaptureOutcome.DefinitivelyFailed
                when operation.Status
                    == ProviderOperationStatus.DefinitivelyFailed
                && payment.Status
                    == PaymentStatus.Failed
                && string.Equals(
                    operation.LastErrorCode,
                    result.ErrorCode,
                    StringComparison.Ordinal)
                && string.Equals(
                    payment.FailureReasonCode,
                    result.ErrorCode,
                    StringComparison.Ordinal) =>
                true,

            PaymentProviderCaptureOutcome.Ambiguous
                when operation.Status
                    == ProviderOperationStatus.Ambiguous
                && payment.Status
                    == PaymentStatus.Processing
                && string.Equals(
                    operation.LastErrorCode,
                    result.ErrorCode,
                    StringComparison.Ordinal) =>
                true,

            _ => false
        };
    }

    private async Task ApplySuccessAsync(
        ProviderCaptureCompletionContext context,
        PaymentProviderCaptureResult result,
        PaymentAggregate payment,
        ProviderOperation operation,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            result.ProviderReference);

        operation.MarkSucceeded(
            result.ProviderReference,
            occurredAtUtc);

        payment.MarkCaptured(occurredAtUtc);

        await _paymentRepository.ApplyAsync(
            payment,
            cancellationToken)
            .ConfigureAwait(false);

        if (!await _ledgerRepository.ExistsAsync(
                "Capture",
                payment.PaymentId,
                cancellationToken)
            .ConfigureAwait(false))
        {
            await _ledgerRepository.AddAsync(
                LedgerTransaction.CreateCapture(
                    Guid.NewGuid(),
                    payment.PaymentId,
                    payment.Amount,
                    payment.Currency,
                    occurredAtUtc),
                cancellationToken)
                .ConfigureAwait(false);
        }

        await _outboxWriter.AddAsync(
            payment.OrderId,
            context.CorrelationId,
            context.CausationId,
            occurredAtUtc,
            "PaymentCaptured.v1",
            new PaymentCapturedV1(
                payment.OrderId,
                payment.PaymentId,
                payment.Amount,
                payment.Currency,
                occurredAtUtc,
                result.ProviderReference),
            context.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplyFailureAsync(
        ProviderCaptureCompletionContext context,
        PaymentProviderCaptureResult result,
        PaymentAggregate payment,
        ProviderOperation operation,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            result.ErrorCode);

        operation.MarkDefinitivelyFailed(
            result.ErrorCode,
            occurredAtUtc);

        payment.MarkFailed(
            result.ErrorCode,
            occurredAtUtc);

        await _paymentRepository.ApplyAsync(
            payment,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            payment.OrderId,
            context.CorrelationId,
            context.CausationId,
            occurredAtUtc,
            "PaymentFailed.v1",
            new PaymentFailedV1(
                payment.OrderId,
                payment.PaymentId,
                result.ErrorCode),
            context.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyAmbiguous(
        PaymentProviderCaptureResult result,
        ProviderOperation operation,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset nextAttemptAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            result.ErrorCode);

        operation.MarkAmbiguous(
            result.ErrorCode,
            occurredAtUtc,
            nextAttemptAtUtc);
    }
}
