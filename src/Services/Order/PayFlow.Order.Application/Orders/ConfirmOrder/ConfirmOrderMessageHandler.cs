using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Domain.Orders;
using PayFlow.Order.Domain.Orders.Events;

namespace PayFlow.Order.Application.Orders.ConfirmOrder;

public sealed class ConfirmOrderMessageHandler
    : IConfirmOrderMessageHandler
{
    private readonly IOrderCommandRepository _orderRepository;
    private readonly IOrderCommandOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmOrderMessageHandler(
        IOrderCommandRepository orderRepository,
        IOrderCommandOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        ConfirmOrderMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "ConfirmOrder payload OrderId must match the envelope AggregateId.",
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

        if (order.Status != OrderStatus.Processing)
        {
            throw new InvalidOperationException(
                $"ConfirmOrder contradicts Order state '{order.Status}'.");
        }

        order.Confirm(
            message.Envelope.OccurredAtUtc);

        var transitionEvent =
            order.DomainEvents
                .OfType<OrderStatusChangedDomainEvent>()
                .Single();

        await _orderRepository.ApplyAsync(
            order,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            transitionEvent,
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

    private static bool IsConsistentReplay(
        OrderStatus status)
    {
        return status is
            OrderStatus.Confirmed
            or OrderStatus.RefundRequested
            or OrderStatus.Refunded
            or OrderStatus.Fulfilled;
    }
}
