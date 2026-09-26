using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed class OrderCommandOutboxWriter
    : IOrderCommandOutboxWriter
{
    private readonly OrderDbContext _dbContext;

    public OrderCommandOutboxWriter(
        OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        OrderStatusChangedDomainEvent domainEvent,
        Guid correlationId,
        Guid causationId,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage =
            OrderProcessingStartedOutboxMapper.Map(
                domainEvent,
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
