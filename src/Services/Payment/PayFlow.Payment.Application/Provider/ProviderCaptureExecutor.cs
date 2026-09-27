using PayFlow.Payment.Application.Abstractions;

namespace PayFlow.Payment.Application.Provider;

public sealed class ProviderCaptureExecutor
{
    private readonly IProviderOperationExecutionRepository _executionRepository;
    private readonly IPaymentProvider _provider;
    private readonly IProviderCaptureOutcomeFinalizer _finalizer;
    private readonly TimeProvider _timeProvider;
    private readonly ProviderCaptureExecutorOptions _options;

    public ProviderCaptureExecutor(
        IProviderOperationExecutionRepository executionRepository,
        IPaymentProvider provider,
        IProviderCaptureOutcomeFinalizer finalizer,
        TimeProvider timeProvider,
        ProviderCaptureExecutorOptions options)
    {
        _executionRepository = executionRepository;
        _provider = provider;
        _finalizer = finalizer;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task<bool> ExecuteNextAsync(
        CancellationToken cancellationToken = default)
    {
        var claimAtUtc =
            _timeProvider.GetUtcNow();

        var workItem =
            await _executionRepository.ClaimNextCaptureAsync(
                claimAtUtc,
                claimAtUtc.Subtract(
                    _options.StaleProcessingAfter),
                cancellationToken)
            .ConfigureAwait(false);

        if (workItem is null)
        {
            return false;
        }

        if (workItem.CorrelationId is not { } correlationId
            || correlationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Claimed provider operation is missing CorrelationId.");
        }

        var result =
            await _provider.CaptureAsync(
                new PaymentProviderCaptureRequest(
                    workItem.PaymentId,
                    workItem.OrderId,
                    workItem.Amount,
                    workItem.Currency,
                    workItem.ProviderIdempotencyKey),
                cancellationToken)
            .ConfigureAwait(false);

        var completedAtUtc =
            _timeProvider.GetUtcNow();

        await _finalizer.FinalizeAsync(
            new ProviderCaptureCompletionContext(
                workItem.PaymentId,
                workItem.OrderId,
                correlationId,
                workItem.CausationId,
                workItem.TraceParent),
            result,
            completedAtUtc,
            completedAtUtc.Add(
                _options.AmbiguousRetryDelay),
            cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
