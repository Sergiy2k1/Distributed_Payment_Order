using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReleasedMessage(
    IntegrationMessageEnvelope Envelope,
    InventoryReleasedV1 Payload);
