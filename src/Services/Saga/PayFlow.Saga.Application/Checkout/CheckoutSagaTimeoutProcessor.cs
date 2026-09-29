using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Checkout;

public sealed class CheckoutSagaTimeoutProcessor
    : ICheckoutSagaTimeoutProcessor
{
    private const string TimeoutReason =
        "CHECKOUT_TIMEOUT";

    private readonly ICheckoutSagaRepository _repository;
    private readonly IPostCaptureCompensationStarter _compensationStarter;

    public CheckoutSagaTimeoutProcessor(
        ICheckoutSagaRepository repository,
        IPostCaptureCompensationStarter compensationStarter)
    {
        _repository = repository;
        _compensationStarter = compensationStarter;
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

            case CheckoutSagaStatus.Started:
            case CheckoutSagaStatus.WaitingForInventory:
            case CheckoutSagaStatus.WaitingForPayment:
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
}
