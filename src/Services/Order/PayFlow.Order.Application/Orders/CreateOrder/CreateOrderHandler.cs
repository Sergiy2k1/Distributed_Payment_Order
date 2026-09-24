using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using OrderAggregate = PayFlow.Order.Domain.Orders.Order;

namespace PayFlow.Order.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICreateOrderIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        ICreateOrderIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        return HandleAsync(
            command,
            idempotencyKey: null,
            cancellationToken);
    }

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Items);

        string? requestHash = null;

        if (idempotencyKey is not null)
        {
            ValidateIdempotencyKey(idempotencyKey);

            requestHash =
                CreateOrderRequestHasher.ComputeHash(command);

            var existing = await _idempotencyRepository
                .GetByKeyAsync(
                    idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return ResolveExisting(
                    existing,
                    requestHash);
            }
        }

        var items = command.Items
            .Select(static item =>
            {
                ArgumentNullException.ThrowIfNull(item);

                return OrderItem.Create(
                    Sku.From(item.Sku),
                    item.Quantity,
                    Money.From(item.UnitPrice, item.Currency));
            })
            .ToArray();

        var createdAtUtc = _clock.UtcNow;
        var order = OrderAggregate.Create(
            OrderId.New(),
            CustomerId.From(command.CustomerId),
            items,
            createdAtUtc);

        var result = new CreateOrderResult(
            order.Id.Value,
            order.Status,
            order.Total.Amount,
            order.Total.Currency);

        await _orderRepository
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

        if (idempotencyKey is not null)
        {
            var idempotencyRecord =
                new CreateOrderIdempotencyRecord(
                    idempotencyKey,
                    requestHash!,
                    result,
                    createdAtUtc);

            await _idempotencyRepository
                .AddAsync(
                    idempotencyRecord,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await _unitOfWork
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CreateOrderIdempotencyConcurrencyException)
            when (idempotencyKey is not null)
        {
            var winner = await _idempotencyRepository
                .GetByKeyAsync(
                    idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (winner is null)
            {
                throw new InvalidOperationException(
                    "The winning idempotency record could not be read after a concurrency conflict.");
            }

            return ResolveExisting(
                winner,
                requestHash!);
        }

        return result;
    }

    private static CreateOrderResult ResolveExisting(
        CreateOrderIdempotencyRecord existing,
        string requestHash)
    {
        if (!string.Equals(
                existing.RequestHash,
                requestHash,
                StringComparison.Ordinal))
        {
            throw new CreateOrderIdempotencyConflictException();
        }

        return existing.Result;
    }

    private static void ValidateIdempotencyKey(
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency-Key cannot be empty.",
                nameof(idempotencyKey));
        }

        if (idempotencyKey.Length > 128)
        {
            throw new ArgumentException(
                "Idempotency-Key cannot exceed 128 characters.",
                nameof(idempotencyKey));
        }
    }
}
