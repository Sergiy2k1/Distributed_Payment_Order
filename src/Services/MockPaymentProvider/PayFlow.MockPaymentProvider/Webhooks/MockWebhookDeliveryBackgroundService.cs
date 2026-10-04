using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PayFlow.MockPaymentProvider.Webhooks;

public sealed partial class MockWebhookDeliveryBackgroundService : BackgroundService
{
    private const string SignatureHeaderName = "X-PayFlow-Signature";

    private readonly MockWebhookDeliveryChannel _channel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MockPaymentProviderOptions _options;
    private readonly MockWebhookSigningOptions _signingOptions;
    private readonly ILogger<MockWebhookDeliveryBackgroundService> _logger;

    public MockWebhookDeliveryBackgroundService(
        MockWebhookDeliveryChannel channel,
        IHttpClientFactory httpClientFactory,
        MockPaymentProviderOptions options,
        MockWebhookSigningOptions signingOptions,
        ILogger<MockWebhookDeliveryBackgroundService> logger)
    {
        _channel = channel;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _signingOptions = signingOptions;
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

                var payload =
                    JsonSerializer.SerializeToUtf8Bytes(
                        delivery.Payload,
                        JsonSerializerOptions.Web);
                var signature =
                    ComputeSignature(
                        payload,
                        _signingOptions.SigningSecret);

                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Post,
                        _options.PaymentWebhookEndpoint);
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/json");
                request.Headers.Add(
                    SignatureHeaderName,
                    signature);

                using var client =
                    _httpClientFactory.CreateClient("payment-webhook-delivery");

                using var response =
                    await client.SendAsync(
                        request,
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

    private static string ComputeSignature(
        ReadOnlySpan<byte> payload,
        string signingSecret)
    {
        using var hmac =
            new HMACSHA256(
                Encoding.UTF8.GetBytes(signingSecret));
        var signature =
            hmac.ComputeHash(payload.ToArray());

        return $"sha256={Convert.ToHexString(signature).ToLowerInvariant()}";
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Delivered mock provider webhook {EventId} for {OperationType}. HTTP status: {StatusCode}.")]
    private static partial void LogDelivered(ILogger logger, Guid eventId, string operationType, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "Mock provider webhook {EventId} for {OperationType} was rejected. HTTP status: {StatusCode}.")]
    private static partial void LogRejected(ILogger logger, Guid eventId, string operationType, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Error, Message = "Mock provider webhook {EventId} for {OperationType} delivery failed.")]
    private static partial void LogFailed(ILogger logger, Guid eventId, string operationType, Exception exception);
}
