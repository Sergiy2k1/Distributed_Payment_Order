using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Observability;
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

                var messageType =
                    GetMessageType(result);

                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var processed = false;

                switch (messageType)
                {
                    case CapturePaymentKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            CapturePaymentKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    CapturePaymentInboxProcessor>();

                        processed = await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    case RefundPaymentKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            RefundPaymentKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    RefundPaymentInboxProcessor>();

                        processed = await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    case ReconcilePaymentKafkaMessageParser.MessageType:
                    {
                        var consumedMessage =
                            ReconcilePaymentKafkaMessageParser.Parse(
                                result,
                                receivedAtUtc);

                        var processor =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ReconcilePaymentInboxProcessor>();

                        processed = await processor.ProcessAsync(
                            consumedMessage,
                            _timeProvider.GetUtcNow(),
                            stoppingToken);

                        break;
                    }

                    default:
                        throw new InvalidDataException(
                            $"Unsupported Payment command '{messageType}'.");
                }

                consumer.Commit(result);

                InboxMetrics.Record(
                    messageType,
                    processed);
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
        EventId = 3301,
        Level = LogLevel.Error,
        Message = "Payment command processing failed. Partition: {Partition}, Offset: {Offset}. Kafka offset was not committed.")]
    private static partial void LogProcessingFailed(
        ILogger logger,
        Exception exception,
        int? partition,
        long? offset);
}
