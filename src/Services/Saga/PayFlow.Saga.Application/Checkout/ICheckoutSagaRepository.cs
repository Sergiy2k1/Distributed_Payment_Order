using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Checkout;

public interface ICheckoutSagaRepository
{
    Task AddAsync(
        CheckoutSaga saga,
        CancellationToken cancellationToken = default);
}
