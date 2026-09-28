using System.Text;
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

                var messageType =
                    GetMessageType(result);

                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                switch (messageType)
                {
                    case ReserveInventoryKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            ReserveInventoryKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ReserveInventoryInboxProcessor>();

                        await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    case ConsumeInventoryKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            ConsumeInventoryKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ConsumeInventoryInboxProcessor>();

                        await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    case ReleaseInventoryKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            ReleaseInventoryKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ReleaseInventoryInboxProcessor>();

                        await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    case RestockInventoryKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            RestockInventoryKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    RestockInventoryInboxProcessor>();

                        await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    default:
                        throw new InvalidDataException(
                            $"Unsupported inventory command '{messageType}'.");
                }

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

    private static string GetMessageType(
        ConsumeResult<string, string> result)
    {
        var headers = result.Message?.Headers
            ?? throw new InvalidDataException(
                "Kafka message headers are missing.");

        var matches = headers
            .Where(header =>
                string.Equals(
                    header.Key,
                    "message-type",
                    StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                "Kafka header 'message-type' must occur exactly once.");
        }

        var bytes = matches[0].GetValueBytes();

        if (bytes is null)
        {
            throw new InvalidDataException(
                "Kafka header 'message-type' is empty.");
        }

        var messageType =
            Encoding.UTF8.GetString(bytes);

        if (string.IsNullOrWhiteSpace(messageType))
        {
            throw new InvalidDataException(
                "Kafka header 'message-type' is empty.");
        }

        return messageType;
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
