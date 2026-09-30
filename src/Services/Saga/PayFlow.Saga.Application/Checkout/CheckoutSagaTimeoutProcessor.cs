using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Payments;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Checkout;

public sealed class CheckoutSagaTimeoutProcessor
    : ICheckoutSagaTimeoutProcessor
{
    private const string TimeoutReason =
        "CHECKOUT_TIMEOUT";

    private const string PaymentReconciliationErrorCode =
        "PAYMENT_OUTCOME_UNKNOWN";

    private const string PaymentReconciliationErrorMessage =
        "Checkout deadline elapsed while payment outcome remained unknown.";

    private readonly ICheckoutSagaRepository _repository;
    private readonly IPostCaptureCompensationStarter _compensationStarter;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;
    private readonly TimeSpan _reconciliationRetryDelay;

    public CheckoutSagaTimeoutProcessor(
        ICheckoutSagaRepository repository,
        IPostCaptureCompensationStarter compensationStarter,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork,
        TimeSpan reconciliationRetryDelay)
    {
        if (reconciliationRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reconciliationRetryDelay),
                reconciliationRetryDelay,
                "Reconciliation retry delay must be greater than zero.");
        }

        _repository = repository;
        _compensationStarter = compensationStarter;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _reconciliationRetryDelay = reconciliationRetryDelay;
    }

    public async Task<IReadOnlyList<CheckoutSagaTimeoutOutcome>> ProcessBatchAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                nameof(nowUtc));
        }

        var overdue =
            await _repository.GetOverdueActiveAsync(
                nowUtc,
                batchSize,
                cancellationToken)
            .ConfigureAwait(false);

        var outcomes =
            new List<CheckoutSagaTimeoutOutcome>(
                overdue.Count);

        foreach (var saga in overdue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            outcomes.Add(
                await ProcessSagaAsync(
                        saga,
                        nowUtc,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        return outcomes;
    }

    private async Task<CheckoutSagaTimeoutOutcome> ProcessSagaAsync(
        CheckoutSaga saga,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        switch (saga.Status)
        {
            case CheckoutSagaStatus.WaitingForInventoryCommit:
            case CheckoutSagaStatus.WaitingForOrderConfirmation:
                await _compensationStarter.StartAsync(
                        new PostCaptureCompensationRequest(
                            saga.OrderId,
                            Guid.NewGuid(),
                            saga.OrderId,
                            Guid.NewGuid(),
                            nowUtc,
                            TimeoutReason,
                            null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new CheckoutSagaTimeoutOutcome(
                    saga.OrderId,
                    CheckoutSagaTimeoutAction.PostCaptureCompensationStarted);

            case CheckoutSagaStatus.WaitingForPayment:
                return await RequestPaymentReconciliationAsync(
                        saga,
                        nowUtc,
                        cancellationToken)
                    .ConfigureAwait(false);

            case CheckoutSagaStatus.Started:
            case CheckoutSagaStatus.WaitingForInventory:
                return new CheckoutSagaTimeoutOutcome(
                    saga.OrderId,
                    CheckoutSagaTimeoutAction.RequiresReconciliation);

            case CheckoutSagaStatus.CompensatingPayment:
            case CheckoutSagaStatus.CompensatingInventory:
            case CheckoutSagaStatus.CompensatingInventoryRestock:
            case CheckoutSagaStatus.WaitingForOrderCancellation:
                return new CheckoutSagaTimeoutOutcome(
                    saga.OrderId,
                    CheckoutSagaTimeoutAction.RecoveryAlreadyInProgress);

            case CheckoutSagaStatus.Completed:
            case CheckoutSagaStatus.CompletedWithBusinessFailure:
            case CheckoutSagaStatus.ManualInterventionRequired:
                return new CheckoutSagaTimeoutOutcome(
                    saga.OrderId,
                    CheckoutSagaTimeoutAction.NoAction);

            default:
                throw new InvalidOperationException(
                    $"Unsupported Checkout Saga timeout state '{saga.Status}'.");
        }
    }

    private async Task<CheckoutSagaTimeoutOutcome> RequestPaymentReconciliationAsync(
        CheckoutSaga saga,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (saga.PaymentId is not { } paymentId)
        {
            throw new InvalidOperationException(
                "WaitingForPayment Saga must contain PaymentId.");
        }

        var reconciliationId =
            Guid.NewGuid();

        saga.SchedulePaymentReconciliation(
            PaymentReconciliationErrorCode,
            PaymentReconciliationErrorMessage,
            nowUtc,
            nowUtc.Add(_reconciliationRetryDelay));

        var trackedSaga =
            await _repository.GetByOrderIdAsync(
                saga.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{saga.OrderId:D}' no longer exists.");

        trackedSaga.SchedulePaymentReconciliation(
            PaymentReconciliationErrorCode,
            PaymentReconciliationErrorMessage,
            nowUtc,
            nowUtc.Add(_reconciliationRetryDelay));

        await _repository.ApplyAsync(
                trackedSaga,
                cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "ReconcilePayment.v1",
                    1,
                    trackedSaga.OrderId,
                    trackedSaga.OrderId,
                    reconciliationId,
                    nowUtc,
                    "Saga",
                    null),
                "payments.commands",
                new ReconcilePaymentV1(
                    trackedSaga.OrderId,
                    paymentId,
                    reconciliationId)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new CheckoutSagaTimeoutOutcome(
            trackedSaga.OrderId,
            CheckoutSagaTimeoutAction.ReconciliationRequested);
    }
}
