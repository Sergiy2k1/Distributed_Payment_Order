using PayFlow.Saga.Application.Orders;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedOrderProcessingStartedMessage(
    OrderProcessingStartedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
