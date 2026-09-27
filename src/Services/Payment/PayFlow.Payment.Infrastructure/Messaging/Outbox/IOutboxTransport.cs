using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Messaging.Outbox;

public interface IOutboxTransport
{
    Task PublishAsync(
        OutboxMessageEntity message,
        CancellationToken cancellationToken = default);
}
