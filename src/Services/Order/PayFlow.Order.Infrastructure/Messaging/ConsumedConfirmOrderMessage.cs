using PayFlow.Order.Application.Orders.ConfirmOrder;

namespace PayFlow.Order.Infrastructure.Messaging;

public sealed record ConsumedConfirmOrderMessage(
    ConfirmOrderMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
