using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Messaging.Outbox;

public interface IOutboxTransport
{
    Task PublishAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken = default);
}
