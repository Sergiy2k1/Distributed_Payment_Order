using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;

namespace PayFlow.Saga.Application.Payments;

public sealed class PaymentRefundRejectedMessageHandler
    : IPaymentRefundRejectedMessageHandler
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PaymentRefundRejectedMessageHandler(
        ICheckoutSagaRepository repository,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PaymentRefundRejectedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        if (message.Payload.OrderId
            != message.Envelope.AggregateId)
        {
            throw new ArgumentException(
                "PaymentRefundRejected OrderId must match AggregateId.",
                nameof(message));
        }

        var saga =
            await _repository.GetByOrderIdAsync(
                message.Payload.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{message.Payload.OrderId:D}' does not exist.");

        saga.RejectPaymentRefund(
            message.Payload.PaymentId,
            message.Payload.RefundId,
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
