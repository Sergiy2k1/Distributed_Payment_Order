using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;

namespace PayFlow.Saga.Application.Orders;

public sealed class OrderConfirmedMessageHandler
    : IOrderConfirmedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaUnitOfWork _unitOfWork;

    public OrderConfirmedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        OrderConfirmedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "OrderConfirmed payload OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                message.Payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{message.Payload.OrderId:D}' does not exist.");

        if (saga.Status == CheckoutSagaStatus.Completed)
        {
            return;
        }

        saga.CompleteSuccessfully(
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
