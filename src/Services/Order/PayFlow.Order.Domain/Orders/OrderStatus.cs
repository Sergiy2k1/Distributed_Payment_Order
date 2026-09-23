namespace PayFlow.Order.Domain.Orders;

public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Confirmed = 2,
    Cancelling = 3,
    Cancelled = 4,
    RefundRequested = 5,
    Refunded = 6,
    Fulfilled = 7
}
