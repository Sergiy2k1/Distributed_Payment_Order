using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Api.Webhooks;

public static class ProviderWebhookEndpoints
{
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
