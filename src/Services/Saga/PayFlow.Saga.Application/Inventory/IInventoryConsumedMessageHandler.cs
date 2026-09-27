namespace PayFlow.Saga.Application.Inventory;

public interface IInventoryConsumedMessageHandler
{
    Task HandleAsync(
        InventoryConsumedMessage message,
        CancellationToken cancellationToken = default);
}
