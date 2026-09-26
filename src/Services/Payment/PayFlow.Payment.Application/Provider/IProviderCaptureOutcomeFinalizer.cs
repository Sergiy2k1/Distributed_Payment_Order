namespace PayFlow.Payment.Application.Provider;

public interface IProviderCaptureOutcomeFinalizer
{
    Task FinalizeAsync(
        ProviderCaptureCompletionContext context,
        PaymentProviderCaptureResult result,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
