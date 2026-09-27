namespace PayFlow.Inventory.Application.Reservations;

public interface IConsumeInventoryMessageHandler
{
    Task HandleAsync(
        ConsumeInventoryMessage message,
        CancellationToken cancellationToken = default);
}
