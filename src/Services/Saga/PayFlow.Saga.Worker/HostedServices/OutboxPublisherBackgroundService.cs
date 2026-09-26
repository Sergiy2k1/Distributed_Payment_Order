using Microsoft.Extensions.Logging;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;

namespace PayFlow.Saga.Worker.HostedServices;

public sealed partial class OutboxPublisherBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxPublisherWorkerOptions _options;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        OutboxPublisherWorkerOptions options,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogPublisherDisabled(_logger);

            return;
        }

        LogPublisherStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var shouldDelay = true;

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var publisher = scope.ServiceProvider
                    .GetRequiredService<OutboxPublisher>();

                var result = await publisher
                    .PublishBatchAsync(stoppingToken)
                    .ConfigureAwait(false);

                shouldDelay = result.ClaimedCount == 0;

                if (result.ClaimedCount > 0)
                {
                    LogBatchProcessed(
                        _logger,
                        result.ClaimedCount,
                        result.PublishedCount,
                        result.FailedCount);
                }
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

                shouldDelay = true;
            }

            if (shouldDelay)
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

        LogPublisherStopped(_logger);
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Saga Outbox publisher is disabled.")]
    private static partial void LogPublisherDisabled(
        ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Saga Outbox publisher started.")]
    private static partial void LogPublisherStarted(
        ILogger logger);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Saga Outbox batch processed. Claimed: {ClaimedCount}, Published: {PublishedCount}, Failed: {FailedCount}.")]
    private static partial void LogBatchProcessed(
        ILogger logger,
        int claimedCount,
        int publishedCount,
        int failedCount);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Error,
        Message = "Saga Outbox publisher iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Information,
        Message = "Saga Outbox publisher stopped.")]
    private static partial void LogPublisherStopped(
        ILogger logger);
}
