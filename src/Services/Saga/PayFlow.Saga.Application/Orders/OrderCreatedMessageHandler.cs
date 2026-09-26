using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Orders;

public sealed class OrderCreatedMessageHandler
    : IOrderCreatedMessageHandler
{
    private readonly ICheckoutSagaRepository _sagaRepository;
    private readonly ISagaUnitOfWork _unitOfWork;
    private readonly TimeSpan _checkoutTimeout;

    public OrderCreatedMessageHandler(
        ICheckoutSagaRepository sagaRepository,
        ISagaUnitOfWork unitOfWork,
        TimeSpan checkoutTimeout)
    {
        if (checkoutTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkoutTimeout),
                checkoutTimeout,
                "Checkout timeout must be greater than zero.");
        }

        _sagaRepository = sagaRepository;
        _unitOfWork = unitOfWork;
        _checkoutTimeout = checkoutTimeout;
    }

    public async Task HandleAsync(
        OrderCreatedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Envelope);
        ArgumentNullException.ThrowIfNull(message.Payload);
        ArgumentNullException.ThrowIfNull(message.Payload.Items);

        if (message.Envelope.AggregateId
            != message.Payload.OrderId)
        {
            throw new ArgumentException(
                "OrderCreated payload OrderId must match the envelope AggregateId.",
                nameof(message));
        }

        var items = message.Payload.Items
            .Select(
                item =>
                {
                    ArgumentNullException.ThrowIfNull(item);

                    return CheckoutSagaItem.Create(
                        item.SkuId,
                        item.Quantity,
                        item.UnitPrice,
                        message.Payload.Currency);
                })
            .ToArray();

        var saga = CheckoutSaga.Start(
            message.Payload.OrderId,
            message.Payload.CustomerId,
            items,
            message.Payload.Currency,
            message.Payload.TotalAmount,
            message.Envelope.OccurredAtUtc,
            message.Envelope.OccurredAtUtc.Add(
                _checkoutTimeout));

        await _sagaRepository
            .AddAsync(
                saga,
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
