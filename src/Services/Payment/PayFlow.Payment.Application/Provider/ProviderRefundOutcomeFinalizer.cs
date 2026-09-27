using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Events;
using PayFlow.Payment.Domain.Ledger;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;

namespace PayFlow.Payment.Application.Provider;

public sealed class ProviderRefundOutcomeFinalizer
    : IProviderRefundOutcomeFinalizer
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProviderOperationRepository _operationRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IPaymentOutboxWriter _outboxWriter;
    private readonly IPaymentUnitOfWork _unitOfWork;

    public ProviderRefundOutcomeFinalizer(
        IPaymentRepository paymentRepository,
        IProviderOperationRepository operationRepository,
        ILedgerRepository ledgerRepository,
        IPaymentOutboxWriter outboxWriter,
        IPaymentUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _operationRepository = operationRepository;
        _ledgerRepository = ledgerRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task FinalizeAsync(
        ProviderRefundCompletionContext context,
        PaymentProviderRefundResult result,
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
                "Refund completion OrderId does not match persisted Payment.");
        }

        var operation =
            await _operationRepository.GetByBusinessOperationAsync(
                "Refund",
                context.PaymentId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Refund operation for Payment '{context.PaymentId:D}' does not exist.");

        if (operation.ProviderOperationId != context.RefundId)
        {
            throw new InvalidOperationException(
                "Refund completion references a different RefundId.");
        }

        if (IsAlreadyApplied(payment, operation, result))
        {
            return;
        }

        switch (result.Outcome)
        {
            case PaymentProviderRefundOutcome.Succeeded:
                await ApplySuccessAsync(
                    context,
                    result,
                    payment,
                    operation,
                    occurredAtUtc,
                    cancellationToken)
                    .ConfigureAwait(false);
                break;

            case PaymentProviderRefundOutcome.DefinitivelyRejected:
                await ApplyRejectedAsync(
                    context,
                    result,
                    payment,
                    operation,
                    occurredAtUtc,
                    cancellationToken)
                    .ConfigureAwait(false);
                break;

            case PaymentProviderRefundOutcome.Ambiguous:
                ArgumentException.ThrowIfNullOrWhiteSpace(result.ErrorCode);
                operation.MarkAmbiguous(
                    result.ErrorCode,
                    occurredAtUtc,
                    nextAttemptAtUtc);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported refund outcome '{result.Outcome}'.");
        }

        await _operationRepository.ApplyAsync(
            operation,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsAlreadyApplied(
        PayFlow.Payment.Domain.Payments.Payment payment,
        ProviderOperation operation,
        PaymentProviderRefundResult result)
    {
        return result.Outcome switch
        {
            PaymentProviderRefundOutcome.Succeeded
                when operation.Status == ProviderOperationStatus.Succeeded
                && payment.Status == PaymentStatus.Refunded =>
                true,

            PaymentProviderRefundOutcome.DefinitivelyRejected
                when operation.Status == ProviderOperationStatus.DefinitivelyFailed
                && payment.Status == PaymentStatus.RefundPending
                && string.Equals(
                    operation.LastErrorCode,
                    result.ErrorCode,
                    StringComparison.Ordinal) =>
                true,

            PaymentProviderRefundOutcome.Ambiguous
                when operation.Status == ProviderOperationStatus.Ambiguous
                && payment.Status == PaymentStatus.RefundPending
                && string.Equals(
                    operation.LastErrorCode,
                    result.ErrorCode,
                    StringComparison.Ordinal) =>
                true,

            _ => false
        };
    }

    private async Task ApplySuccessAsync(
        ProviderRefundCompletionContext context,
        PaymentProviderRefundResult result,
        PayFlow.Payment.Domain.Payments.Payment payment,
        ProviderOperation operation,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result.ProviderReference);

        operation.MarkSucceeded(
            result.ProviderReference,
            occurredAtUtc);

        payment.MarkRefunded(occurredAtUtc);

        await _paymentRepository.ApplyAsync(
            payment,
            cancellationToken)
            .ConfigureAwait(false);

        if (!await _ledgerRepository.ExistsAsync(
                "Refund",
                context.RefundId,
                cancellationToken)
            .ConfigureAwait(false))
        {
            await _ledgerRepository.AddAsync(
                LedgerTransaction.CreateRefund(
                    Guid.NewGuid(),
                    context.RefundId,
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
            "PaymentRefunded.v1",
            new PaymentRefundedV1(
                payment.OrderId,
                payment.PaymentId,
                context.RefundId,
                payment.Amount,
                payment.Currency,
                occurredAtUtc),
            context.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplyRejectedAsync(
        ProviderRefundCompletionContext context,
        PaymentProviderRefundResult result,
        PayFlow.Payment.Domain.Payments.Payment payment,
        ProviderOperation operation,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result.ErrorCode);

        operation.MarkDefinitivelyFailed(
            result.ErrorCode,
            occurredAtUtc);

        await _outboxWriter.AddAsync(
            payment.OrderId,
            context.CorrelationId,
            context.CausationId,
            occurredAtUtc,
            "PaymentRefundRejected.v1",
            new PaymentRefundRejectedV1(
                payment.OrderId,
                payment.PaymentId,
                context.RefundId,
                result.ErrorCode),
            context.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);
    }
}
