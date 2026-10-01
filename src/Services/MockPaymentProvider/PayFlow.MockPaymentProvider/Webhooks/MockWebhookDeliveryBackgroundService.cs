using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed partial class MockWebhookDeliveryBackgroundService : BackgroundService
{
    private readonly MockWebhookDeliveryChannel _channel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MockPaymentProviderOptions _options;
    private readonly ILogger<MockWebhookDeliveryBackgroundService> _logger;

    public MockWebhookDeliveryBackgroundService(
        MockWebhookDeliveryChannel channel,
        IHttpClientFactory httpClientFactory,
        MockPaymentProviderOptions options,
        ILogger<MockWebhookDeliveryBackgroundService> logger)
    {
        _channel = channel;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var delivery in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (delivery.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(delivery.Delay, stoppingToken);
                }

                using var client =
                    _httpClientFactory.CreateClient("payment-webhook-delivery");

                using var response =
                    await client.PostAsJsonAsync(
                        _options.PaymentWebhookEndpoint,
                        delivery.Payload,
                        stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    LogDelivered(
                        _logger,
                        delivery.Payload.EventId,
                        delivery.Payload.OperationType,
                        response.StatusCode);
                }
                else
                {
                    LogRejected(
                        _logger,
                        delivery.Payload.EventId,
                        delivery.Payload.OperationType,
                        response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailed(
                    _logger,
                    delivery.Payload.EventId,
                    delivery.Payload.OperationType,
                    exception);
            }
        }
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Delivered mock provider webhook {EventId} for {OperationType}. HTTP status: {StatusCode}.")]
    private static partial void LogDelivered(ILogger logger, Guid eventId, string operationType, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "Mock provider webhook {EventId} for {OperationType} was rejected. HTTP status: {StatusCode}.")]
    private static partial void LogRejected(ILogger logger, Guid eventId, string operationType, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Error, Message = "Mock provider webhook {EventId} for {OperationType} delivery failed.")]
    private static partial void LogFailed(ILogger logger, Guid eventId, string operationType, Exception exception);
}
