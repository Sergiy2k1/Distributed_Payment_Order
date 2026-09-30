using PayFlow.Payment.Application.Reconciliation;

namespace PayFlow.Payment.Infrastructure.Messaging;

public sealed record ConsumedReconcilePaymentMessage(
    ReconcilePaymentMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
