using PayFlow.Saga.Application.Inventory;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedInventoryReservedMessage(
    InventoryReservedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
