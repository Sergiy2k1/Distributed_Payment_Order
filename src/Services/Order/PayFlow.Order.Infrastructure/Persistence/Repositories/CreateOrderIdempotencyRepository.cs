using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Persistence.Repositories;

public sealed class CreateOrderIdempotencyRepository
    : ICreateOrderIdempotencyRepository
{
    private readonly OrderDbContext _dbContext;

    public CreateOrderIdempotencyRepository(
        OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateOrderIdempotencyRecord?> GetByKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            idempotencyKey);

        var entity = await _dbContext.CreateOrderIdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        if (!Enum.TryParse<OrderStatus>(
                entity.ResponseStatus,
                ignoreCase: false,
                out var status))
        {
            throw new InvalidOperationException(
                $"Persisted Order status '{entity.ResponseStatus}' is invalid.");
        }

        return new CreateOrderIdempotencyRecord(
            entity.IdempotencyKey,
            entity.RequestHash,
            new CreateOrderResult(
                entity.OrderId,
                status,
                entity.ResponseTotalAmount,
                entity.ResponseCurrency),
            entity.CreatedAtUtc);
    }

    public async Task AddAsync(
        CreateOrderIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            record.IdempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            record.RequestHash);

        var entity = new CreateOrderIdempotencyEntity
        {
            IdempotencyKey = record.IdempotencyKey,
            RequestHash = record.RequestHash,
            OrderId = record.Result.OrderId,
            ResponseStatus = record.Result.Status.ToString(),
            ResponseTotalAmount = record.Result.TotalAmount,
            ResponseCurrency = record.Result.Currency,
            CreatedAtUtc = record.CreatedAtUtc
        };

        await _dbContext.CreateOrderIdempotencyRecords
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }
}
