using Microsoft.EntityFrameworkCore;
using Npgsql;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;

namespace PayFlow.Order.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private const string IdempotencyPrimaryKey =
        "PK_order_idempotency";

    private readonly OrderDbContext _dbContext;

    public EfUnitOfWork(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (IsIdempotencyKeyConflict(exception))
        {
            _dbContext.ChangeTracker.Clear();

            throw new CreateOrderIdempotencyConcurrencyException(
                exception);
        }
    }

    private static bool IsIdempotencyKeyConflict(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyPrimaryKey
        };
    }
}
