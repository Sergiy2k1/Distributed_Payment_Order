namespace PayFlow.Saga.Application.Inventory;

public interface IInventoryReservationRejectedMessageHandler
{
    Task HandleAsync(
        InventoryReservationRejectedMessage message,
        CancellationToken cancellationToken = default);
}
