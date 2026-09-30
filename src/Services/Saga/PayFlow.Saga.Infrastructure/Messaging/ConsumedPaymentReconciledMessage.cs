using PayFlow.Saga.Application.Payments;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedPaymentReconciledMessage(
    PaymentReconciledMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
