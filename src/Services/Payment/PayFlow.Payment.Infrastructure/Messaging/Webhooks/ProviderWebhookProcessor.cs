using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Infrastructure.Persistence;

namespace PayFlow.Payment.Infrastructure.Messaging.Webhooks;

public sealed class ProviderWebhookProcessor
{
    private const string EventType =
        "PaymentProviderResult.v1";

    private readonly PaymentDbContext _dbContext;
    private readonly ProviderWebhookInboxRepository _webhookRepository;
    private readonly IProviderCaptureOutcomeFinalizer _captureFinalizer;
    private readonly IProviderRefundOutcomeFinalizer _refundFinalizer;

    public ProviderWebhookProcessor(
        PaymentDbContext dbContext,
        ProviderWebhookInboxRepository webhookRepository,
        IProviderCaptureOutcomeFinalizer captureFinalizer,
        IProviderRefundOutcomeFinalizer refundFinalizer)
    {
        _dbContext = dbContext;
        _webhookRepository = webhookRepository;
        _captureFinalizer = captureFinalizer;
        _refundFinalizer = refundFinalizer;
    }

    public async Task<bool> ProcessAsync(
        Guid eventId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId cannot be empty.",
                nameof(eventId));
        }

        EnsureUtc(
            processedAtUtc,
            nameof(processedAtUtc));

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

        var webhook =
            await _webhookRepository.GetByIdAsync(
                eventId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Provider webhook '{eventId:D}' does not exist.");

        if (webhook.ProcessedAtUtc is not null)
        {
            await transaction.CommitAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return false;
        }

        if (!string.Equals(
                webhook.EventType,
                EventType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported provider webhook event type '{webhook.EventType}'.");
        }

        var payment =
            await _dbContext.Payments
                .AsNoTracking()
                .Where(
                    entity =>
                        entity.PaymentId
                        == webhook.PaymentId)
                .Select(
                    entity =>
                        new
                        {
                            entity.OrderId,
                            entity.UpdatedAtUtc
                        })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Payment '{webhook.PaymentId:D}' does not exist.");

        var operation =
            await _dbContext.ProviderOperations
                .AsNoTracking()
                .Where(
                    entity =>
                        entity.OperationType
                        == webhook.OperationType
                        && entity.BusinessOperationId
                        == webhook.PaymentId)
                .Select(
                    entity =>
                        new
                        {
                            entity.ProviderOperationId,
                            entity.UpdatedAtUtc,
                            entity.CorrelationId,
                            entity.TraceParent
                        })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"{webhook.OperationType} operation for Payment '{webhook.PaymentId:D}' does not exist.");

        if (processedAtUtc < webhook.ReceivedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processedAtUtc),
                processedAtUtc,
                "Processed timestamp cannot be earlier than webhook receipt.");
        }

        var effectiveOccurredAtUtc =
            Max(
                webhook.OccurredAtUtc,
                payment.UpdatedAtUtc,
                operation.UpdatedAtUtc);

        if (processedAtUtc < effectiveOccurredAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processedAtUtc),
                processedAtUtc,
                "Processed timestamp cannot be earlier than the effective provider transition.");
        }

        var correlationId =
            operation.CorrelationId
            ?? payment.OrderId;

        switch (webhook.OperationType)
        {
            case "Capture":
                await ProcessCaptureAsync(
                        webhook.Outcome,
                        webhook.ProviderReference,
                        webhook.ErrorCode,
                        webhook.PaymentId,
                        payment.OrderId,
                        correlationId,
                        webhook.EventId,
                        operation.TraceParent,
                        effectiveOccurredAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "Refund":
                if (webhook.RefundId is not { } refundId
                    || refundId == Guid.Empty)
                {
                    throw new InvalidOperationException(
                        "Refund webhook must contain RefundId.");
                }

                if (operation.ProviderOperationId
                    != refundId)
                {
                    throw new InvalidOperationException(
                        "Refund webhook references a different persisted Refund operation.");
                }

                await ProcessRefundAsync(
                        webhook.Outcome,
                        webhook.ProviderReference,
                        webhook.ErrorCode,
                        refundId,
                        webhook.PaymentId,
                        payment.OrderId,
                        correlationId,
                        webhook.EventId,
                        operation.TraceParent,
                        effectiveOccurredAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported provider webhook operation type '{webhook.OperationType}'.");
        }

        await _webhookRepository.MarkProcessedAsync(
                webhook.EventId,
                processedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private async Task ProcessCaptureAsync(
        string outcome,
        string? providerReference,
        string? errorCode,
        Guid paymentId,
        Guid orderId,
        Guid correlationId,
        Guid eventId,
        string? traceParent,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var result =
            outcome switch
            {
                "Succeeded" =>
                    PaymentProviderCaptureResult.Succeeded(
                        providerReference
                        ?? throw new InvalidOperationException(
                            "Succeeded capture webhook must contain ProviderReference.")),

                "DefinitivelyFailed" =>
                    PaymentProviderCaptureResult.DefinitivelyFailed(
                        errorCode
                        ?? throw new InvalidOperationException(
                            "Failed capture webhook must contain ErrorCode.")),

                _ => throw new InvalidOperationException(
                    $"Unsupported capture webhook outcome '{outcome}'.")
            };

        await _captureFinalizer.FinalizeAsync(
                new ProviderCaptureCompletionContext(
                    paymentId,
                    orderId,
                    correlationId,
                    eventId,
                    traceParent),
                result,
                occurredAtUtc,
                occurredAtUtc.AddMinutes(1),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProcessRefundAsync(
        string outcome,
        string? providerReference,
        string? errorCode,
        Guid refundId,
        Guid paymentId,
        Guid orderId,
        Guid correlationId,
        Guid eventId,
        string? traceParent,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var result =
            outcome switch
            {
                "Succeeded" =>
                    PaymentProviderRefundResult.Succeeded(
                        providerReference
                        ?? throw new InvalidOperationException(
                            "Succeeded refund webhook must contain ProviderReference.")),

                "DefinitivelyRejected" =>
                    PaymentProviderRefundResult.DefinitivelyRejected(
                        errorCode
                        ?? throw new InvalidOperationException(
                            "Rejected refund webhook must contain ErrorCode.")),

                _ => throw new InvalidOperationException(
                    $"Unsupported refund webhook outcome '{outcome}'.")
            };

        await _refundFinalizer.FinalizeAsync(
                new ProviderRefundCompletionContext(
                    refundId,
                    paymentId,
                    orderId,
                    correlationId,
                    eventId,
                    traceParent),
                result,
                occurredAtUtc,
                occurredAtUtc.AddMinutes(1),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second,
        DateTimeOffset third)
    {
        var max =
            first > second
                ? first
                : second;

        return max > third
            ? max
            : third;
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
