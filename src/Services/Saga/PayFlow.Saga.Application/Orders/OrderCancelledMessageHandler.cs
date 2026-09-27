using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Orders;

public sealed class OrderCancelledMessageHandler
    : IOrderCancelledMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaUnitOfWork _unitOfWork;

    public OrderCancelledMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        OrderCancelledMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "OrderCancelled payload OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                message.Payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{message.Payload.OrderId:D}' does not exist.");

        if (saga.Status == CheckoutSagaStatus.CompletedWithBusinessFailure)
        {
            return;
        }

        saga.CompleteWithBusinessFailure(
            message.Envelope.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
