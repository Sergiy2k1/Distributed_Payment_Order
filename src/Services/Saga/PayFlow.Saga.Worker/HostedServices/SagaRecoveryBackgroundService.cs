using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Saga.Application.Checkout;

namespace PayFlow.Saga.Worker.HostedServices;

public sealed partial class SagaRecoveryBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SagaRecoveryWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SagaRecoveryBackgroundService> _logger;

    public SagaRecoveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        SagaRecoveryWorkerOptions options,
        TimeProvider timeProvider,
        ILogger<SagaRecoveryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogRecoveryDisabled(_logger);

            return;
        }

        LogRecoveryStarted(
            _logger,
            _options.PollInterval,
            _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessIterationAsync(
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

            try
            {
                await Task.Delay(
                        _options.PollInterval,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogRecoveryStopped(_logger);
    }

    private async Task ProcessIterationAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var processor =
            scope.ServiceProvider
                .GetRequiredService<
                    ICheckoutSagaTimeoutProcessor>();

        var outcomes =
            await processor.ProcessBatchAsync(
                    _timeProvider.GetUtcNow(),
                    _options.BatchSize,
                    cancellationToken)
                .ConfigureAwait(false);

        if (outcomes.Count == 0)
        {
            return;
        }

        var compensationsStarted =
            outcomes.Count(
                outcome =>
                    outcome.Action
                    == CheckoutSagaTimeoutAction.PostCaptureCompensationStarted);

        var requiresReconciliation =
            outcomes.Count(
                outcome =>
                    outcome.Action
                    == CheckoutSagaTimeoutAction.RequiresReconciliation);

        var recoveryAlreadyInProgress =
            outcomes.Count(
                outcome =>
                    outcome.Action
                    == CheckoutSagaTimeoutAction.RecoveryAlreadyInProgress);

        var noAction =
            outcomes.Count(
                outcome =>
                    outcome.Action
                    == CheckoutSagaTimeoutAction.NoAction);

        LogBatchProcessed(
            _logger,
            outcomes.Count,
            compensationsStarted,
            requiresReconciliation,
            recoveryAlreadyInProgress,
            noAction);

        if (requiresReconciliation > 0)
        {
            LogReconciliationRequired(
                _logger,
                requiresReconciliation);
        }
    }

    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Saga recovery worker is disabled.")]
    private static partial void LogRecoveryDisabled(
        ILogger logger);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Saga recovery worker started. PollInterval: {PollInterval}, BatchSize: {BatchSize}.")]
    private static partial void LogRecoveryStarted(
        ILogger logger,
        TimeSpan pollInterval,
        int batchSize);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "Saga recovery batch processed. Total: {TotalCount}, CompensationStarted: {CompensationStarted}, RequiresReconciliation: {RequiresReconciliation}, RecoveryAlreadyInProgress: {RecoveryAlreadyInProgress}, NoAction: {NoAction}.")]
    private static partial void LogBatchProcessed(
        ILogger logger,
        int totalCount,
        int compensationStarted,
        int requiresReconciliation,
        int recoveryAlreadyInProgress,
        int noAction);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Warning,
        Message = "Saga recovery found {Count} overdue checkout Saga(s) that require reconciliation before a safe action can be chosen.")]
    private static partial void LogReconciliationRequired(
        ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Error,
        Message = "Saga recovery iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Information,
        Message = "Saga recovery worker stopped.")]
    private static partial void LogRecoveryStopped(
        ILogger logger);
}
