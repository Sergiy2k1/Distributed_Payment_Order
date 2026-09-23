using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Infrastructure.Persistence.Mapping;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
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
}
