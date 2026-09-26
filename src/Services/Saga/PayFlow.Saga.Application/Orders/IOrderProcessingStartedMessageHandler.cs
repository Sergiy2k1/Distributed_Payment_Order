namespace PayFlow.Saga.Application.Orders;

public interface IOrderProcessingStartedMessageHandler
{
    Task HandleAsync(
        OrderProcessingStartedMessage message,
        CancellationToken cancellationToken = default);
}
