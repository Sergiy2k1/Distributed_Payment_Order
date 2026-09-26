using PayFlow.Inventory.Application.Reservations;

namespace PayFlow.Inventory.Infrastructure.Messaging;

public sealed record ConsumedReserveInventoryMessage(
    ReserveInventoryMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
