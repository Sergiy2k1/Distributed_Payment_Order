namespace PayFlow.Inventory.Infrastructure.Messaging.Outbox;

public sealed record OutboxBacklogSnapshot(
    long PendingMessages,
    long OldestPendingAgeSeconds);
