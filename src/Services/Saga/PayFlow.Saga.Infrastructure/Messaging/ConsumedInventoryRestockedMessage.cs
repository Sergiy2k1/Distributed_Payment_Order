using PayFlow.Saga.Application.Inventory;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedInventoryRestockedMessage(
    InventoryRestockedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
