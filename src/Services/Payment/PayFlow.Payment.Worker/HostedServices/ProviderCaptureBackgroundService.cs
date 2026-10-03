using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Observability;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Provider;

namespace PayFlow.Payment.Worker.HostedServices;

public sealed partial class ProviderCaptureBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderCaptureWorkerOptions _workerOptions;
    private readonly ProviderCaptureExecutorOptions _executorOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProviderCaptureBackgroundService> _logger;

    public ProviderCaptureBackgroundService(
        IServiceScopeFactory scopeFactory,
        ProviderCaptureWorkerOptions workerOptions,
        ProviderCaptureExecutorOptions executorOptions,
        TimeProvider timeProvider,
        ILogger<ProviderCaptureBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _workerOptions = workerOptions;
        _executorOptions = executorOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_workerOptions.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;

            try
            {
                processed =
                    await ExecuteIterationAsync(
                        stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogIterationFailed(
                    _logger,
                    exception);
            }

            if (!processed)
            {
                await Task.Delay(
                    _workerOptions.PollInterval,
                    stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> ExecuteIterationAsync(
        CancellationToken cancellationToken)
    {
        if (await TryExecuteCaptureAsync(
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        return await TryExecuteRefundAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryExecuteCaptureAsync(
        CancellationToken cancellationToken)
    {
        var claimAtUtc =
            _timeProvider.GetUtcNow();

        ProviderCaptureWorkItem? workItem;

        await using (var claimScope =
            _scopeFactory.CreateAsyncScope())
        {
            var repository =
                claimScope.ServiceProvider
                    .GetRequiredService<
                        IProviderOperationExecutionRepository>();

            workItem =
                await repository.ClaimNextCaptureAsync(
                    claimAtUtc,
                    claimAtUtc.Subtract(
                        _executorOptions.StaleProcessingAfter),
                    cancellationToken)
                .ConfigureAwait(false);
        }

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

        PaymentProviderCaptureResult result;

        await using (var providerScope =
            _scopeFactory.CreateAsyncScope())
        {
            var provider =
                providerScope.ServiceProvider
                    .GetRequiredService<IPaymentProvider>();

            result =
                await provider.CaptureAsync(
                    new PaymentProviderCaptureRequest(
                        workItem.PaymentId,
                        workItem.OrderId,
                        workItem.Amount,
                        workItem.Currency,
                        workItem.ProviderIdempotencyKey),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var completedAtUtc =
            _timeProvider.GetUtcNow();

        await using (var finalizationScope =
            _scopeFactory.CreateAsyncScope())
        {
            var finalizer =
                finalizationScope.ServiceProvider
                    .GetRequiredService<
                        IProviderCaptureOutcomeFinalizer>();

            await finalizer.FinalizeAsync(
                new ProviderCaptureCompletionContext(
                    workItem.PaymentId,
                    workItem.OrderId,
                    correlationId,
                    workItem.CausationId,
                    workItem.TraceParent),
                result,
                completedAtUtc,
                completedAtUtc.Add(
                    _executorOptions.AmbiguousRetryDelay),
                cancellationToken)
                .ConfigureAwait(false);
        }

        PaymentBusinessMetrics.RecordProviderOperation(
            "capture",
            result.Outcome.ToString());

        return true;
    }

    private async Task<bool> TryExecuteRefundAsync(
        CancellationToken cancellationToken)
    {
        var claimAtUtc =
            _timeProvider.GetUtcNow();

        ProviderRefundWorkItem? workItem;

        await using (var claimScope =
            _scopeFactory.CreateAsyncScope())
        {
            var repository =
                claimScope.ServiceProvider
                    .GetRequiredService<
                        IProviderOperationExecutionRepository>();

            workItem =
                await repository.ClaimNextRefundAsync(
                    claimAtUtc,
                    claimAtUtc.Subtract(
                        _executorOptions.StaleProcessingAfter),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (workItem is null)
        {
            return false;
        }

        if (workItem.CorrelationId is not { } correlationId
            || correlationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Claimed refund operation is missing CorrelationId.");
        }

        PaymentProviderRefundResult result;

        await using (var providerScope =
            _scopeFactory.CreateAsyncScope())
        {
            var provider =
                providerScope.ServiceProvider
                    .GetRequiredService<IPaymentProvider>();

            result =
                await provider.RefundAsync(
                    new PaymentProviderRefundRequest(
                        workItem.RefundId,
                        workItem.PaymentId,
                        workItem.OrderId,
                        workItem.Amount,
                        workItem.Currency,
                        workItem.ProviderIdempotencyKey),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var completedAtUtc =
            _timeProvider.GetUtcNow();

        await using (var finalizationScope =
            _scopeFactory.CreateAsyncScope())
        {
            var finalizer =
                finalizationScope.ServiceProvider
                    .GetRequiredService<
                        IProviderRefundOutcomeFinalizer>();

            await finalizer.FinalizeAsync(
                new ProviderRefundCompletionContext(
                    workItem.RefundId,
                    workItem.PaymentId,
                    workItem.OrderId,
                    correlationId,
                    workItem.CausationId,
                    workItem.TraceParent),
                result,
                completedAtUtc,
                completedAtUtc.Add(
                    _executorOptions.AmbiguousRetryDelay),
                cancellationToken)
                .ConfigureAwait(false);
        }

        PaymentBusinessMetrics.RecordProviderOperation(
            "refund",
            result.Outcome.ToString());

        return true;
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Error,
        Message = "Payment provider capture executor iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);
}
