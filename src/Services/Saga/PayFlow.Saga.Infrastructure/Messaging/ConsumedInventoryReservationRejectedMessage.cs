using PayFlow.Saga.Application.Inventory;

namespace PayFlow.Saga.Infrastructure.Messaging;

public sealed record ConsumedInventoryReservationRejectedMessage(
    InventoryReservationRejectedMessage Message,
    string SourceTopic,
    int SourcePartition,
    long SourceOffset,
    DateTimeOffset ReceivedAtUtc);
