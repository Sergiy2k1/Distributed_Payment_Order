using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Orders.BeginOrderProcessing;

public sealed class BeginOrderProcessingMessageHandler
    : IBeginOrderProcessingMessageHandler
{
    private readonly IOrderCommandRepository _orderRepository;
    private readonly IOrderCommandOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;

    public BeginOrderProcessingMessageHandler(
        IOrderCommandRepository orderRepository,
        IOrderCommandOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        BeginOrderProcessingMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Envelope);
        ArgumentNullException.ThrowIfNull(message.Payload);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "BeginOrderProcessing payload OrderId must match the envelope AggregateId.",
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

        if (IsConsistentReplay(order.Status))
        {
            return;
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException(
                $"BeginOrderProcessing contradicts Order state '{order.Status}'.");
        }

        order.StartProcessing(
            message.Envelope.OccurredAtUtc);

        var transitionEvent =
            order.DomainEvents
                .OfType<OrderStatusChangedDomainEvent>()
                .Single();

        await _orderRepository
            .ApplyAsync(
                order,
                cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter
            .AddAsync(
                transitionEvent,
                message.Envelope.CorrelationId,
                message.Envelope.MessageId,
                message.Envelope.TraceParent,
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        order.ClearDomainEvents();
    }

    private static bool IsConsistentReplay(
        OrderStatus status)
    {
        return status is
            OrderStatus.Processing
            or OrderStatus.Confirmed
            or OrderStatus.RefundRequested
            or OrderStatus.Refunded
            or OrderStatus.Fulfilled;
    }
}
