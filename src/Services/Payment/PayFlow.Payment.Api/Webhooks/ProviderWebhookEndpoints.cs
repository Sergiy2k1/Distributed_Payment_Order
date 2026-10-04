using System.Text.Json;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Api.Webhooks;

public static class ProviderWebhookEndpoints
{
    private const int MaximumPayloadBytes = 64 * 1024;

    public static async Task<IResult> ReceiveSignedAsync(
        HttpRequest httpRequest,
        ProviderWebhookInboxRepository repository,
        ProviderWebhookSigningOptions signingOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (httpRequest.ContentLength
            is > MaximumPayloadBytes)
        {
            return Results.StatusCode(
                StatusCodes.Status413PayloadTooLarge);
        }

        await using var payloadStream =
            new MemoryStream();

        await httpRequest.Body.CopyToAsync(
                payloadStream,
                cancellationToken)
            .ConfigureAwait(false);

        if (payloadStream.Length > MaximumPayloadBytes)
        {
            return Results.StatusCode(
                StatusCodes.Status413PayloadTooLarge);
        }

        var payload = payloadStream.ToArray();
        var signatureHeader =
            httpRequest.Headers[
                ProviderWebhookSignatureVerifier.HeaderName]
                .ToString();

        if (!ProviderWebhookSignatureVerifier.IsValid(
                payload,
                signatureHeader,
                signingOptions.SigningSecret))
        {
            return Results.Unauthorized();
        }

        ProviderWebhookRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<ProviderWebhookRequest>(
                payload,
                JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return Results.BadRequest(
                new
                {
                    error = "Webhook payload is not valid JSON."
                });
        }

        if (request is null)
        {
            return Results.BadRequest(
                new
                {
                    error = "Webhook payload is required."
                });
        }

        return await ReceiveAsync(
                request,
                repository,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<IResult> ReceiveAsync(
        ProviderWebhookRequest request,
        ProviderWebhookInboxRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validationProblem =
            Validate(request);

        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var receivedAtUtc =
            timeProvider.GetUtcNow();

        var webhook =
            new ProviderWebhookInboxEntity
            {
                EventId = request.EventId,
                EventType = request.EventType,
                OperationType = request.OperationType,
                PaymentId = request.PaymentId,
                RefundId = request.RefundId,
                Outcome = request.Outcome,
                ProviderReference = request.ProviderReference,
                ErrorCode = request.ErrorCode,
                OccurredAtUtc = request.OccurredAtUtc,
                ReceivedAtUtc = receivedAtUtc,
                ProcessedAtUtc = null,
                PayloadHash =
                    ProviderWebhookPayloadHasher.Compute(
                        request)
            };

        var result =
            await repository.TryInsertAsync(
                    webhook,
                    cancellationToken)
                .ConfigureAwait(false);

        return result switch
        {
            ProviderWebhookInsertResult.Inserted =>
                Results.Accepted(
                    value:
                        new
                        {
                            eventId = request.EventId,
                            status = "accepted"
                        }),

            ProviderWebhookInsertResult.Duplicate =>
                Results.Ok(
                    new
                    {
                        eventId = request.EventId,
                        status = "duplicate"
                    }),

            ProviderWebhookInsertResult.Conflict =>
                Results.Conflict(
                    new
                    {
                        eventId = request.EventId,
                        status = "conflict"
                    }),

            _ => throw new InvalidOperationException(
                $"Unsupported webhook insert result '{result}'.")
        };
    }

    private static IResult? Validate(
        ProviderWebhookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EventId == Guid.Empty
            || request.PaymentId == Guid.Empty)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "EventId and PaymentId must be non-empty."
                });
        }

        if (string.IsNullOrWhiteSpace(
                request.EventType)
            || string.IsNullOrWhiteSpace(
                request.OperationType)
            || string.IsNullOrWhiteSpace(
                request.Outcome))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "EventType, OperationType and Outcome are required."
                });
        }

        if (request.OccurredAtUtc.Offset
            != TimeSpan.Zero)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "OccurredAtUtc must use UTC offset."
                });
        }

        if (string.Equals(
                request.OperationType,
                "Refund",
                StringComparison.Ordinal)
            && request.RefundId is null)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "RefundId is required for Refund webhooks."
                });
        }

        if (!string.Equals(
                request.OperationType,
                "Capture",
                StringComparison.Ordinal)
            && !string.Equals(
                request.OperationType,
                "Refund",
                StringComparison.Ordinal))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "OperationType must be Capture or Refund."
                });
        }

        return null;
    }
}
