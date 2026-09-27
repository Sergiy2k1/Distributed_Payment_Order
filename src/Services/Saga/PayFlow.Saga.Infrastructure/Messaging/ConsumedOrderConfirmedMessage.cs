using PayFlow.Saga.Application.Orders;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedOrderConfirmedMessage(
    OrderConfirmedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
