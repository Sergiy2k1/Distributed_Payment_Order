using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Mappers;

public static class CheckoutSagaEntityMapper
{
    public static CheckoutSaga ToDomain(
        CheckoutSagaEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!Enum.TryParse<CheckoutSagaStatus>(
                entity.Status,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted Checkout Saga status '{entity.Status}' is invalid.");
        }

        PostCaptureCompensationMode? compensationMode = null;

        if (entity.PostCaptureCompensationMode is not null)
        {
            if (!Enum.TryParse<PostCaptureCompensationMode>(
                    entity.PostCaptureCompensationMode,
                    ignoreCase: false,
                    out var parsedMode))
            {
                throw new InvalidOperationException(
                    $"Persisted post-capture compensation mode '{entity.PostCaptureCompensationMode}' is invalid.");
            }

            compensationMode = parsedMode;
        }

        var items = entity.Items
            .OrderBy(item => item.Position)
            .Select(
                item => CheckoutSagaItem.Create(
                    item.SkuId,
                    item.Quantity,
                    item.UnitPrice,
                    item.Currency))
            .ToArray();

        return CheckoutSaga.Rehydrate(
            entity.OrderId,
            entity.CustomerId,
            items,
            entity.Currency,
            entity.TotalAmount,
            status,
            entity.StartedAtUtc,
            entity.UpdatedAtUtc,
            entity.DeadlineAtUtc,
            entity.ReservationId,
            entity.ReservationExpiresAtUtc,
            entity.PaymentId,
            entity.RefundId,
            compensationMode,
            entity.RestockOperationId,
            entity.RetryCount,
            entity.NextAttemptAtUtc,
            entity.LastTechnicalErrorCode,
            entity.LastTechnicalErrorMessage,
            entity.Version);
    }

    public static CheckoutSagaEntity ToEntity(
        CheckoutSaga saga)
    {
        ArgumentNullException.ThrowIfNull(saga);

        return new CheckoutSagaEntity
        {
            OrderId = saga.OrderId,
            CustomerId = saga.CustomerId,
            Status = saga.Status.ToString(),
            Currency = saga.Currency,
            TotalAmount = saga.TotalAmount,
            StartedAtUtc = saga.StartedAtUtc,
            UpdatedAtUtc = saga.UpdatedAtUtc,
            DeadlineAtUtc = saga.DeadlineAtUtc,
            ReservationId = saga.ReservationId,
            ReservationExpiresAtUtc =
                saga.ReservationExpiresAtUtc,
            PaymentId = saga.PaymentId,
            RefundId = saga.RefundId,
            PostCaptureCompensationMode =
                saga.PostCaptureCompensationMode?.ToString(),
            RestockOperationId = saga.RestockOperationId,
            RetryCount = saga.RetryCount,
            NextAttemptAtUtc = saga.NextAttemptAtUtc,
            LastTechnicalErrorCode =
                saga.LastTechnicalErrorCode,
            LastTechnicalErrorMessage =
                saga.LastTechnicalErrorMessage,
            Version = saga.Version,
            Items = saga.Items
                .Select(
                    (item, position) =>
                        new CheckoutSagaItemEntity
                        {
                            OrderId = saga.OrderId,
                            Position = position,
                            SkuId = item.SkuId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Currency = item.Currency
                        })
                .ToList()
        };
    }
}
