namespace PayFlow.Inventory.Application.Reservations;

public interface IReserveInventoryMessageHandler
{
    Task HandleAsync(
        ReserveInventoryMessage message,
        CancellationToken cancellationToken = default);
}
