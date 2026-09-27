using PayFlow.Saga.Application.Payments;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedPaymentCapturedMessage(
    PaymentCapturedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
