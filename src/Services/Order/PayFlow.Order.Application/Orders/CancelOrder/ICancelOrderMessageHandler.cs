namespace PayFlow.Order.Application.Orders.CancelOrder;

public interface ICancelOrderMessageHandler
{
    Task HandleAsync(
        CancelOrderMessage message,
        CancellationToken cancellationToken = default);
}
