using PayFlow.Inventory.Application.Reservations;

namespace PayFlow.Inventory.Infrastructure.Messaging;

public sealed record ConsumedConsumeInventoryMessage(
    ConsumeInventoryMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
