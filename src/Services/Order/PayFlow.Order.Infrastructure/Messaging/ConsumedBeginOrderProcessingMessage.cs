using PayFlow.Order.Application.Orders.BeginOrderProcessing;

namespace PayFlow.Order.Infrastructure.Messaging;

public sealed record ConsumedBeginOrderProcessingMessage(
    BeginOrderProcessingMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
