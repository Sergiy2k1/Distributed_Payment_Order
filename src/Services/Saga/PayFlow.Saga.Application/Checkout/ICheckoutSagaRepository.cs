using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Checkout;

public interface ICheckoutSagaRepository
{
    Task AddAsync(
        CheckoutSaga saga,
        CancellationToken cancellationToken = default);

    Task<CheckoutSaga?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        CheckoutSaga saga,
        CancellationToken cancellationToken = default);
}
