using PayFlow.Order.Infrastructure.Messaging.Outbox;

namespace PayFlow.Order.Api.HostedServices;

public sealed class OutboxPublisherBackgroundService
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
            _logger.LogInformation(
                "Order Outbox publisher is disabled.");

            return;
        }

        if (!HasConfiguredTransport())
        {
            _logger.LogCritical(
                "Order Outbox publisher is enabled, but no IOutboxTransport is configured.");

            return;
        }

        _logger.LogInformation(
            "Order Outbox publisher started.");

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
                    .PublishBatchAsync(stoppingToken);

                shouldDelay = result.ClaimedCount == 0;

                if (result.ClaimedCount > 0)
                {
                    _logger.LogInformation(
                        "Order Outbox batch processed. Claimed: {ClaimedCount}, Published: {PublishedCount}, Failed: {FailedCount}.",
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
                _logger.LogError(
                    exception,
                    "Order Outbox publisher iteration failed.");

                shouldDelay = true;
            }

            if (shouldDelay)
            {
                try
                {
                    await Task.Delay(
                        _options.PollInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation(
            "Order Outbox publisher stopped.");
    }

    private bool HasConfiguredTransport()
    {
        using var scope = _scopeFactory.CreateScope();

        return scope.ServiceProvider
            .GetService<IOutboxTransport>() is not null;
    }
}
