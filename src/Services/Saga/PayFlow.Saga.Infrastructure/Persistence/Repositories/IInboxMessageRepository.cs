using PayFlow.Saga.Infrastructure.Persistence.Entities;

namespace PayFlow.Saga.Infrastructure.Persistence.Repositories;

public interface IInboxMessageRepository
{
    Task<bool> TryInsertAsync(
        InboxMessageEntity message,
        CancellationToken cancellationToken = default);
}
