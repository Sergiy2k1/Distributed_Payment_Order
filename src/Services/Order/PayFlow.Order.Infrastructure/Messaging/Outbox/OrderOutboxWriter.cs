using System.Diagnostics;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders.Events;
using PayFlow.Order.Infrastructure.Persistence;

namespace PayFlow.Order.Infrastructure.Messaging.Outbox;

public sealed class OrderOutboxWriter : IOutboxWriter
{
    private readonly OrderDbContext _dbContext;

    public OrderOutboxWriter(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var outboxMessage = domainEvent switch
        {
            OrderCreatedDomainEvent orderCreated =>
                OrderCreatedOutboxMapper.Map(
                    orderCreated,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Activity.Current?.Id),
            _ => throw new NotSupportedException(
                $"Domain event '{domainEvent.GetType().Name}' does not have an Outbox mapping.")
        };

        await _dbContext.OutboxMessages
            .AddAsync(outboxMessage, cancellationToken)
            .ConfigureAwait(false);
    }
}
