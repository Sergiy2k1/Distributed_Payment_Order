using Confluent.Kafka;
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

                    var consumedMessage =
                        OrderCreatedKafkaMessageParser.Parse(
                            result,
                            _timeProvider.GetUtcNow());

                    await using var scope =
                        _scopeFactory.CreateAsyncScope();

                    var processor =
                        scope.ServiceProvider
                            .GetRequiredService<
                                OrderCreatedInboxProcessor>();

                    var processed =
                        await processor.ProcessAsync(
                                consumedMessage,
                                _timeProvider.GetUtcNow(),
                                stoppingToken)
                            .ConfigureAwait(false);

                    consumer.Commit(result);

                    LogMessageCommitted(
                        _logger,
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        consumedMessage.Message.Envelope.MessageId,
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
        Message = "Saga OrderCreated consumer started. Topic: {Topic}, Group: {ConsumerGroup}.")]
    private static partial void LogConsumerStarted(
        ILogger logger,
        string topic,
        string consumerGroup);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Saga OrderCreated message committed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, MessageId: {MessageId}, Processed: {Processed}.")]
    private static partial void LogMessageCommitted(
        ILogger logger,
        string topic,
        int partition,
        long offset,
        Guid messageId,
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
        Message = "Saga OrderCreated processing failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        string? topic,
        int? partition,
        long? offset);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Information,
        Message = "Saga OrderCreated consumer stopped.")]
    private static partial void LogConsumerStopped(
        ILogger logger);
}
