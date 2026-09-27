using PayFlow.Order.Application.Orders.CancelOrder;

namespace PayFlow.Order.Infrastructure.Messaging;

public sealed record ConsumedCancelOrderMessage(
    CancelOrderMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
