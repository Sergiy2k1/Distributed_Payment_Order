namespace PayFlow.Inventory.Application.Reservations;

public interface IReleaseInventoryMessageHandler
{
    Task HandleAsync(
        ReleaseInventoryMessage message,
        CancellationToken cancellationToken = default);
}
