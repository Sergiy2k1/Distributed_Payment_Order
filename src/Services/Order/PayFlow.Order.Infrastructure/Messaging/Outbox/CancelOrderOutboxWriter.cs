using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed class CancelOrderOutboxWriter
    : ICancelOrderOutboxWriter
{
    private readonly OrderDbContext _dbContext;

    public CancelOrderOutboxWriter(
        OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        OrderStatusChangedDomainEvent domainEvent,
        string reasonCode,
        Guid correlationId,
        Guid causationId,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage =
            OrderCancelledOutboxMapper.Map(
                domainEvent,
                reasonCode,
                Guid.NewGuid(),
                Guid.NewGuid(),
                correlationId,
                causationId,
                traceParent);

        await _dbContext.OutboxMessages
            .AddAsync(
                outboxMessage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
