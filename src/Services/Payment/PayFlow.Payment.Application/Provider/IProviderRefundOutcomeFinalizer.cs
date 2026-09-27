namespace PayFlow.Payment.Application.Provider;

public interface IProviderRefundOutcomeFinalizer
{
    Task FinalizeAsync(
        ProviderRefundCompletionContext context,
        PaymentProviderRefundResult result,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
