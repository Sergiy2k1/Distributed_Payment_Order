using System.Net;
using System.Net.Http.Json;
using PayFlow.Payment.Application.Provider;

namespace PayFlow.Payment.Infrastructure.Provider;

public sealed class HttpPaymentProvider
    : IPaymentProvider
{
    private readonly HttpClient _httpClient;

    public HttpPaymentProvider(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentProviderCaptureResult> CaptureAsync(
        PaymentProviderCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "payments/capture")
            {
                Content = JsonContent.Create(
                    new
                    {
                        request.PaymentId,
                        request.OrderId,
                        request.Amount,
                        request.Currency
                    })
            };

        message.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            request.IdempotencyKey);

        try
        {
            using var response =
                await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var payload =
                    await response.Content
                        .ReadFromJsonAsync<CaptureResponse>(
                            cancellationToken)
                        .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(
                        payload?.ProviderReference))
                {
                    return PaymentProviderCaptureResult.Ambiguous(
                        "PROVIDER_RESPONSE_MISSING_REFERENCE");
                }

                return PaymentProviderCaptureResult.Succeeded(
                    payload.ProviderReference);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.PaymentRequired
                or HttpStatusCode.Conflict
                or HttpStatusCode.UnprocessableEntity)
            {
                return PaymentProviderCaptureResult.DefinitivelyFailed(
                    $"HTTP_{(int)response.StatusCode}");
            }

            return PaymentProviderCaptureResult.Ambiguous(
                $"HTTP_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return PaymentProviderCaptureResult.Ambiguous(
                "TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return PaymentProviderCaptureResult.Ambiguous(
                "NETWORK_ERROR");
        }
    }

    public async Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "payments/refund")
            {
                Content = JsonContent.Create(
                    new
                    {
                        request.PaymentId,
                        request.OrderId,
                        request.RefundId,
                        request.Amount,
                        request.Currency
                    })
            };

        message.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            request.IdempotencyKey);

        try
        {
            using var response =
                await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var payload =
                    await response.Content
                        .ReadFromJsonAsync<CaptureResponse>(
                            cancellationToken)
                        .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(
                        payload?.ProviderReference))
                {
                    return PaymentProviderRefundResult.Ambiguous(
                        "PROVIDER_RESPONSE_MISSING_REFERENCE");
                }

                return PaymentProviderRefundResult.Succeeded(
                    payload.ProviderReference);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.PaymentRequired
                or HttpStatusCode.Conflict
                or HttpStatusCode.UnprocessableEntity)
            {
                return PaymentProviderRefundResult.DefinitivelyRejected(
                    $"HTTP_{(int)response.StatusCode}");
            }

            return PaymentProviderRefundResult.Ambiguous(
                $"HTTP_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return PaymentProviderRefundResult.Ambiguous(
                "TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return PaymentProviderRefundResult.Ambiguous(
                "NETWORK_ERROR");
        }
    }

    private sealed record CaptureResponse(
        string ProviderReference);
}
