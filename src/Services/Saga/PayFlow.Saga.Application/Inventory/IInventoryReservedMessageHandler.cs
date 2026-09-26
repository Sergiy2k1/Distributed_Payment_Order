namespace PayFlow.Saga.Application.Inventory;

public interface IInventoryReservedMessageHandler
{
    Task HandleAsync(
        InventoryReservedMessage message,
        CancellationToken cancellationToken = default);
}
