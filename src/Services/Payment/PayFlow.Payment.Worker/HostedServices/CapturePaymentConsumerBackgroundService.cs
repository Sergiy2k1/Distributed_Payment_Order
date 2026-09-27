using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Payment.Infrastructure.Messaging;
using PayFlow.Payment.Infrastructure.Messaging.Kafka;

namespace PayFlow.Payment.Worker.HostedServices;

public sealed partial class CapturePaymentConsumerBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CapturePaymentConsumerBackgroundService> _logger;

    public CapturePaymentConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        PaymentWorkerOptions options,
        TimeProvider timeProvider,
        ILogger<CapturePaymentConsumerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var config =
            new ConsumerConfig
            {
                BootstrapServers =
                    _options.BootstrapServers,
                GroupId =
                    _options.ConsumerGroup,
                AutoOffsetReset =
                    AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false
            };

        using var consumer =
            new ConsumerBuilder<string, string>(
                config)
            .Build();

        consumer.Subscribe(
            CapturePaymentKafkaMessageParser.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;

            try
            {
                result = consumer.Consume(
                    stoppingToken);

                var receivedAtUtc =
                    _timeProvider.GetUtcNow();

                var consumedMessage =
                    CapturePaymentKafkaMessageParser.Parse(
                        result,
                        receivedAtUtc);

                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var processor =
                    scope.ServiceProvider
                        .GetRequiredService<
                            CapturePaymentInboxProcessor>();

                await processor.ProcessAsync(
                    consumedMessage,
                    _timeProvider.GetUtcNow(),
                    stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogProcessingFailed(
                    _logger,
                    exception,
                    result?.Partition.Value,
                    result?.Offset.Value);

                await Task.Delay(
                    _options.ConsumeErrorDelay,
                    stoppingToken);
            }
        }

        consumer.Close();
    }

    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Error,
        Message = "Payment command processing failed. Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        int? partition,
        long? offset);
}
