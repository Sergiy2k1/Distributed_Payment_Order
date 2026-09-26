using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Mappers;

public static class CheckoutSagaEntityMapper
{
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
