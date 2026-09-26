using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Kafka;

namespace PayFlow.Saga.Worker.HostedServices;

public sealed partial class OrderCreatedConsumerBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderCreatedConsumerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderCreatedConsumerBackgroundService> _logger;

    public OrderCreatedConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        OrderCreatedConsumerOptions options,
        TimeProvider timeProvider,
        ILogger<OrderCreatedConsumerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var consumerConfig =
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
                consumerConfig)
            .Build();

        consumer.Subscribe(
            OrderCreatedKafkaMessageParser.Topic);

        LogConsumerStarted(
            _logger,
            OrderCreatedKafkaMessageParser.Topic,
            _options.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(
                        stoppingToken);

                    var messageType =
                        GetMessageType(result);

                    var processed =
                        await DispatchAsync(
                                result,
                                messageType,
                                stoppingToken)
                            .ConfigureAwait(false);

                    consumer.Commit(result);

                    LogMessageCommitted(
                        _logger,
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        messageType,
                        processed);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException exception)
                {
                    LogConsumeFailed(
                        _logger,
                        exception);

                    await DelayAfterFailureAsync(
                            stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogProcessingFailed(
                        _logger,
                        exception,
                        result?.Topic,
                        result?.Partition.Value,
                        result?.Offset.Value);

                    await DelayAfterFailureAsync(
                            stoppingToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            consumer.Close();

            LogConsumerStopped(_logger);
        }
    }

    private async Task<bool> DispatchAsync(
        ConsumeResult<string, string> result,
        string messageType,
        CancellationToken cancellationToken)
    {
        var receivedAtUtc =
            _timeProvider.GetUtcNow();

        await using var scope =
            _scopeFactory.CreateAsyncScope();

        return messageType switch
        {
            OrderCreatedKafkaMessageParser.MessageType =>
                await ProcessOrderCreatedAsync(
                        scope.ServiceProvider,
                        result,
                        receivedAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false),

            OrderProcessingStartedKafkaMessageParser.MessageType =>
                await ProcessOrderProcessingStartedAsync(
                        scope.ServiceProvider,
                        result,
                        receivedAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false),

            _ => throw new InvalidDataException(
                $"Unsupported orders.events message type '{messageType}'.")
        };
    }

    private async Task<bool> ProcessOrderCreatedAsync(
        IServiceProvider serviceProvider,
        ConsumeResult<string, string> result,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken)
    {
        var consumedMessage =
            OrderCreatedKafkaMessageParser.Parse(
                result,
                receivedAtUtc);

        var processor =
            serviceProvider.GetRequiredService<
                OrderCreatedInboxProcessor>();

        return await processor
            .ProcessAsync(
                consumedMessage,
                _timeProvider.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ProcessOrderProcessingStartedAsync(
        IServiceProvider serviceProvider,
        ConsumeResult<string, string> result,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken)
    {
        var consumedMessage =
            OrderProcessingStartedKafkaMessageParser.Parse(
                result,
                receivedAtUtc);

        var processor =
            serviceProvider.GetRequiredService<
                OrderProcessingStartedInboxProcessor>();

        return await processor
            .ProcessAsync(
                consumedMessage,
                _timeProvider.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
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

    private async Task DelayAfterFailureAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(
                    _options.ConsumeErrorDelay,
                    stoppingToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Saga orders.events consumer started. Topic: {Topic}, Group: {ConsumerGroup}.")]
    private static partial void LogConsumerStarted(
        ILogger logger,
        string topic,
        string consumerGroup);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Saga event committed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, MessageType: {MessageType}, Processed: {Processed}.")]
    private static partial void LogMessageCommitted(
        ILogger logger,
        string topic,
        int partition,
        long offset,
        string messageType,
        bool processed);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Error,
        Message = "Saga Kafka consume operation failed.")]
    private static partial void LogConsumeFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Error,
        Message = "Saga event processing failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        string? topic,
        int? partition,
        long? offset);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Information,
        Message = "Saga orders.events consumer stopped.")]
    private static partial void LogConsumerStopped(
        ILogger logger);
}
