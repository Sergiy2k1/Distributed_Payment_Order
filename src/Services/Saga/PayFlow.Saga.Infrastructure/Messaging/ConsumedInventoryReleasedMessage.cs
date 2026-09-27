using PayFlow.Saga.Application.Inventory;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedInventoryReleasedMessage(
    InventoryReleasedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
