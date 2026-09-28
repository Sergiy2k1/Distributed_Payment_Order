using PayFlow.Saga.Application.Payments;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedPaymentRefundedMessage(
    PaymentRefundedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
