using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Observability;
using PayFlow.Payment.Infrastructure.Messaging.Outbox;

namespace PayFlow.Payment.Worker.HostedServices;

public sealed partial class OutboxPublisherBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxPublisherWorkerOptions _options;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        OutboxPublisherWorkerOptions options,
        ILogger<OutboxPublisherBackgroundService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
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
                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<IOutboxMessageRepository>();

                var result =
                    await publisher.PublishBatchAsync(
                        stoppingToken);

                var backlog = await repository
                    .GetBacklogSnapshotAsync(
                        _timeProvider.GetUtcNow(),
                        stoppingToken)
                    .ConfigureAwait(false);

                OutboxMetrics.Observe(
                    backlog.PendingMessages,
                    backlog.OldestPendingAgeSeconds);

                shouldDelay =
                    result.ClaimedCount == 0;
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
        EventId = 3201,
        Level = LogLevel.Error,
        Message = "Payment Outbox publisher iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);
}
