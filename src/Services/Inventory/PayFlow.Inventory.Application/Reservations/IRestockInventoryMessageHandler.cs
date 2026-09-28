namespace PayFlow.Inventory.Application.Reservations;

public interface IRestockInventoryMessageHandler
{
    Task HandleAsync(
        RestockInventoryMessage message,
        CancellationToken cancellationToken = default);
}
