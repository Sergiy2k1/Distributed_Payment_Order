namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed class CreateOrderIdempotencyConcurrencyException
    : Exception
{
    public CreateOrderIdempotencyConcurrencyException(
        Exception innerException)
        : base(
            "A concurrent CreateOrder request won the idempotency race.",
            innerException)
    {
    }
}
