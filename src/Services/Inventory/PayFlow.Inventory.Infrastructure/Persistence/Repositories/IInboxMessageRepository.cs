using PayFlow.Inventory.Infrastructure.Persistence.Entities;

namespace PayFlow.Inventory.Infrastructure.Persistence.Repositories;

public interface IInboxMessageRepository
{
    Task<bool> TryInsertAsync(
        InboxMessageEntity message,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        string consumerName,
        Guid messageId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);
}
