using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public interface IOutboxTransport
{
    Task PublishAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken = default);
}
