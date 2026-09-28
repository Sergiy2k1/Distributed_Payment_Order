namespace PayFlow.Saga.Application.Inventory;

public interface IInventoryRestockedMessageHandler
{
    Task HandleAsync(
        InventoryRestockedMessage message,
        CancellationToken cancellationToken = default);
}
