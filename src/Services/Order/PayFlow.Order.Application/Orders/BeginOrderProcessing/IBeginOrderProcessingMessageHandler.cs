namespace PayFlow.Order.Application.Orders.BeginOrderProcessing;

public interface IBeginOrderProcessingMessageHandler
{
    Task HandleAsync(
        BeginOrderProcessingMessage message,
        CancellationToken cancellationToken = default);
}
