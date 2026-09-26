using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Inventory.Infrastructure.Messaging.Outbox;

namespace PayFlow.Inventory.Worker.HostedServices;

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
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var shouldDelay = true;

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var publisher =
                    scope.ServiceProvider
                        .GetRequiredService<OutboxPublisher>();

                var result =
                    await publisher.PublishBatchAsync(
                        stoppingToken);

                shouldDelay = result.ClaimedCount == 0;
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

            if (shouldDelay)
            {
                await Task.Delay(
                    _options.PollInterval,
                    stoppingToken);
            }
        }
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Error,
        Message = "Inventory Outbox publisher iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);
}
