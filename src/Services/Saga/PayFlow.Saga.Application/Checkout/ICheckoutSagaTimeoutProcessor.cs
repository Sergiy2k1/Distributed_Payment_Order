namespace PayFlow.Saga.Application.Checkout;

public interface ICheckoutSagaTimeoutProcessor
{
    Task<IReadOnlyList<CheckoutSagaTimeoutOutcome>> ProcessBatchAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}
