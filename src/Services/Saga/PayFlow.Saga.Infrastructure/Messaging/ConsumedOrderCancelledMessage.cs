using PayFlow.Saga.Application.Orders;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedOrderCancelledMessage(
    OrderCancelledMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
