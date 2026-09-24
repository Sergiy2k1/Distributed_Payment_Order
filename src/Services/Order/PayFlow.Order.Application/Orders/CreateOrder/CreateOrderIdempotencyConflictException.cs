namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed class CreateOrderIdempotencyConflictException
    : InvalidOperationException
{
    public CreateOrderIdempotencyConflictException()
        : base(
            "The Idempotency-Key has already been used with a different request payload.")
    {
    }
}
