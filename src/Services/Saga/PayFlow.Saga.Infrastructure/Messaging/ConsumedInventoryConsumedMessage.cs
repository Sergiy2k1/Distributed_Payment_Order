using PayFlow.Saga.Application.Inventory;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedInventoryConsumedMessage(
    InventoryConsumedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
