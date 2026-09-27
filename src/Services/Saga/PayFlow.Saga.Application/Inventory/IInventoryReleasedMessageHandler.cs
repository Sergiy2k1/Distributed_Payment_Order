namespace PayFlow.Saga.Application.Inventory;

public interface IInventoryReleasedMessageHandler
{
    Task HandleAsync(
        InventoryReleasedMessage message,
        CancellationToken cancellationToken = default);
}
