namespace PayFlow.Order.Application.Orders.ConfirmOrder;

public interface IConfirmOrderMessageHandler
{
    Task HandleAsync(
        ConfirmOrderMessage message,
        CancellationToken cancellationToken = default);
}
