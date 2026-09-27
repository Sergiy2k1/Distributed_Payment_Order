using PayFlow.Payment.Application.Refund;

namespace PayFlow.Payment.Infrastructure.Messaging;

public sealed record ConsumedRefundPaymentMessage(
    RefundPaymentMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
