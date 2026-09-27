namespace PayFlow.Saga.Application.Orders;

public interface IOrderConfirmedMessageHandler
{
    Task HandleAsync(
        OrderConfirmedMessage message,
        CancellationToken cancellationToken = default);
}
