using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Inventory.Infrastructure.Messaging;
using PayFlow.Inventory.Infrastructure.Messaging.Kafka;

namespace PayFlow.Inventory.Worker.HostedServices;

public sealed partial class ReserveInventoryConsumerBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InventoryWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReserveInventoryConsumerBackgroundService> _logger;

    public ReserveInventoryConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        InventoryWorkerOptions options,
        TimeProvider timeProvider,
        ILogger<ReserveInventoryConsumerBackgroundService> logger)
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
            ReserveInventoryKafkaMessageParser.Topic);

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
                    ReserveInventoryKafkaMessageParser.Parse(
                        result,
                        receivedAtUtc);

                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var processor =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ReserveInventoryInboxProcessor>();

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
        EventId = 2201,
        Level = LogLevel.Error,
        Message = "Inventory command processing failed. Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        int? partition,
        long? offset);
}
