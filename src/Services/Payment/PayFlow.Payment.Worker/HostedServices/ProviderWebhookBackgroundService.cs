using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;

namespace PayFlow.Payment.Worker.HostedServices;

public sealed partial class ProviderWebhookBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderWebhookWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProviderWebhookBackgroundService> _logger;

    public ProviderWebhookBackgroundService(
        IServiceScopeFactory scopeFactory,
        ProviderWebhookWorkerOptions options,
        TimeProvider timeProvider,
        ILogger<ProviderWebhookBackgroundService> logger)
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
            LogDisabled(_logger);
            return;
        }

        LogStarted(
            _logger,
            _options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;

            try
            {
                processed =
                    await ProcessNextAsync(
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
        }

        LogStopped(_logger);
    }

    private async Task<bool> ProcessNextAsync(
        CancellationToken cancellationToken)
    {
        Guid? eventId;

        await using (var lookupScope =
            _scopeFactory.CreateAsyncScope())
        {
            var repository =
                lookupScope.ServiceProvider
                    .GetRequiredService<
                        ProviderWebhookInboxRepository>();

            eventId =
                await repository
                    .GetNextUnprocessedEventIdAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        if (eventId is null)
        {
            return false;
        }

        await using var processingScope =
            _scopeFactory.CreateAsyncScope();

        var processor =
            processingScope.ServiceProvider
                .GetRequiredService<
                    ProviderWebhookProcessor>();

        var processed =
            await processor.ProcessAsync(
                    eventId.Value,
                    _timeProvider.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);

        if (processed)
        {
            LogProcessed(
                _logger,
                eventId.Value);
        }

        return true;
    }

    [LoggerMessage(
        EventId = 3200,
        Level = LogLevel.Information,
        Message = "Provider webhook worker is disabled.")]
    private static partial void LogDisabled(
        ILogger logger);

    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Information,
        Message = "Provider webhook worker started. PollInterval: {PollInterval}.")]
    private static partial void LogStarted(
        ILogger logger,
        TimeSpan pollInterval);

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Information,
        Message = "Processed provider webhook {EventId}.")]
    private static partial void LogProcessed(
        ILogger logger,
        Guid eventId);

    [LoggerMessage(
        EventId = 3203,
        Level = LogLevel.Error,
        Message = "Provider webhook worker iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3204,
        Level = LogLevel.Information,
        Message = "Provider webhook worker stopped.")]
    private static partial void LogStopped(
        ILogger logger);
}
