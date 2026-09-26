using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public interface IOutboxTransport
{
    Task PublishAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken = default);
}
