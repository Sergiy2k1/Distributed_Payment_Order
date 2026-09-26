using Confluent.Kafka;
using PayFlow.Order.Infrastructure.Messaging;
using PayFlow.Order.Infrastructure.Messaging.Kafka;

namespace PayFlow.Order.Api.HostedServices;

public sealed partial class OrderCommandsConsumerBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderCommandsConsumerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderCommandsConsumerBackgroundService> _logger;

    public OrderCommandsConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        OrderCommandsConsumerOptions options,
        TimeProvider timeProvider,
        ILogger<OrderCommandsConsumerBackgroundService> logger)
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
            BeginOrderProcessingKafkaMessageParser.Topic);

        LogConsumerStarted(
            _logger,
            BeginOrderProcessingKafkaMessageParser.Topic,
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
                        BeginOrderProcessingKafkaMessageParser.Parse(
                            result,
                            _timeProvider.GetUtcNow());

                    await using var scope =
                        _scopeFactory.CreateAsyncScope();

                    var processor =
                        scope.ServiceProvider
                            .GetRequiredService<
                                BeginOrderProcessingInboxProcessor>();

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
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Order command consumer started. Topic: {Topic}, Group: {ConsumerGroup}.")]
    private static partial void LogConsumerStarted(
        ILogger logger,
        string topic,
        string consumerGroup);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Order command committed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, MessageId: {MessageId}, Processed: {Processed}.")]
    private static partial void LogMessageCommitted(
        ILogger logger,
        string topic,
        int partition,
        long offset,
        Guid messageId,
        bool processed);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Error,
        Message = "Order Kafka consume operation failed.")]
    private static partial void LogConsumeFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Error,
        Message = "Order command processing failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        string? topic,
        int? partition,
        long? offset);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Information,
        Message = "Order command consumer stopped.")]
    private static partial void LogConsumerStopped(
        ILogger logger);
}
