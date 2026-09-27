using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Payment.Application.Provider;

namespace PayFlow.Payment.Worker.HostedServices;

public sealed partial class ProviderCaptureBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderCaptureWorkerOptions _options;
    private readonly ILogger<ProviderCaptureBackgroundService> _logger;

    public ProviderCaptureBackgroundService(
        IServiceScopeFactory scopeFactory,
        ProviderCaptureWorkerOptions options,
        ILogger<ProviderCaptureBackgroundService> logger)
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
            var processed = false;

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var executor =
                    scope.ServiceProvider
                        .GetRequiredService<ProviderCaptureExecutor>();

                processed =
                    await executor.ExecuteNextAsync(
                        stoppingToken);
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
                    _options.PollInterval,
                    stoppingToken);
            }
        }
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Error,
        Message = "Payment provider capture executor iteration failed.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        Exception exception);
}
