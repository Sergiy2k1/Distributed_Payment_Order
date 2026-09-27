using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Orders.CancelOrder;

public sealed class CancelOrderMessageHandler
    : ICancelOrderMessageHandler
{
    private readonly IOrderCommandRepository _orderRepository;
    private readonly ICancelOrderOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderMessageHandler(
        IOrderCommandRepository orderRepository,
        ICancelOrderOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        CancelOrderMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "CancelOrder payload OrderId must match the envelope AggregateId.",
                nameof(message));
        }

        var orderId =
            OrderId.From(message.Payload.OrderId);

        var order = await _orderRepository
            .GetByIdAsync(
                orderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Order '{orderId}' does not exist.");

        if (order.Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (order.Status is not
            (OrderStatus.Pending or OrderStatus.Processing))
        {
            throw new InvalidOperationException(
                $"CancelOrder contradicts Order state '{order.Status}'.");
        }

        order.BeginCancellation(
            message.Envelope.OccurredAtUtc);
        order.CompleteCancellation(
            message.Envelope.OccurredAtUtc);

        var cancelledEvent =
            order.DomainEvents
                .OfType<OrderStatusChangedDomainEvent>()
                .Single(domainEvent =>
                    domainEvent.CurrentStatus
                    == OrderStatus.Cancelled);

        await _orderRepository.ApplyAsync(
            order,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            cancelledEvent,
            message.Payload.ReasonCode,
            message.Envelope.CorrelationId,
            message.Envelope.MessageId,
            message.Envelope.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);

        order.ClearDomainEvents();
    }
}
