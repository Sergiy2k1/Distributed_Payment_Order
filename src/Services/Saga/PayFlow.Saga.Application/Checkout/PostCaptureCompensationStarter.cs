using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Payments;

namespace PayFlow.Saga.Application.Checkout;

public sealed class PostCaptureCompensationStarter
    : IPostCaptureCompensationStarter
{
    private readonly ICheckoutSagaRepository _repository;
    private readonly ISagaOutboxWriter _outboxWriter;
    private readonly ISagaUnitOfWork _unitOfWork;

    public PostCaptureCompensationStarter(
        ICheckoutSagaRepository repository,
        ISagaOutboxWriter outboxWriter,
        ISagaUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task StartAsync(
        PostCaptureCompensationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ReasonCode);

        var saga =
            await _repository.GetByOrderIdAsync(
                request.OrderId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Checkout Saga for Order '{request.OrderId:D}' does not exist.");

        if (saga.Status == global::PayFlow.Saga.Domain.Checkout.CheckoutSagaStatus.CompensatingPayment)
        {
            if (saga.RefundId == request.RefundId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Post-capture compensation already uses a different RefundId.");
        }

        if (saga.PaymentId is not { } paymentId)
        {
            throw new InvalidOperationException(
                "Post-capture compensation requires persisted PaymentId.");
        }

        saga.BeginPostCaptureCompensation(
            request.RefundId,
            request.OccurredAtUtc);

        await _repository.ApplyAsync(
            saga,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            new OutgoingIntegrationMessage(
                new IntegrationMessageEnvelope(
                    Guid.NewGuid(),
                    "RefundPayment.v1",
                    1,
                    saga.OrderId,
                    request.CorrelationId,
                    request.CausationId,
                    request.OccurredAtUtc,
                    "Saga",
                    request.TraceParent),
                "payments.commands",
                new RefundPaymentV1(
                    saga.OrderId,
                    paymentId,
                    request.RefundId,
                    saga.TotalAmount,
                    saga.Currency,
                    request.ReasonCode)),
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
