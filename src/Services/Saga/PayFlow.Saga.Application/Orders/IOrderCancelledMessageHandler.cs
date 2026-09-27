namespace PayFlow.Saga.Application.Orders;

public interface IOrderCancelledMessageHandler
{
    Task HandleAsync(
        OrderCancelledMessage message,
        CancellationToken cancellationToken = default);
}
