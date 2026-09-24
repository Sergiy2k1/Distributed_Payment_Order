namespace PayFlow.Order.Application.Orders.CreateOrder;

public interface ICreateOrderIdempotencyRepository
{
    Task<CreateOrderIdempotencyRecord?> GetByKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CreateOrderIdempotencyRecord record,
        CancellationToken cancellationToken = default);
}
