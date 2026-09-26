namespace PayFlow.Saga.Application.Orders;

public interface IOrderCreatedMessageHandler
{
    Task HandleAsync(
        OrderCreatedMessage message,
        CancellationToken cancellationToken = default);
}
