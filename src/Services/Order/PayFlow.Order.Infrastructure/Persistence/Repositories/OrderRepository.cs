using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Persistence.Mapping;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository
    : IOrderRepository,
      IOrderCommandRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        OrderAggregate order,
        CancellationToken cancellationToken = default)
    {
        var entity = OrderEntityMapper.ToEntity(order);

        await _dbContext.Orders
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OrderAggregate?> GetByIdAsync(
        OrderId orderId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order => order.Id == orderId.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : OrderEntityMapper.ToDomain(entity);
    }

    public Task ApplyAsync(
        OrderAggregate order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = _dbContext.Orders.Local
            .SingleOrDefault(
                tracked => tracked.Id == order.Id.Value)
            ?? throw new InvalidOperationException(
                "Order must be loaded by this repository before applying a transition.");

        entity.Status = order.Status.ToString();
        entity.UpdatedAtUtc = order.UpdatedAtUtc;
        entity.Version = order.Version;

        return Task.CompletedTask;
    }
}
