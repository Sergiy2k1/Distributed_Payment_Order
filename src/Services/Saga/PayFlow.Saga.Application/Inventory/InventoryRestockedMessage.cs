using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryRestockedMessage(
    IntegrationMessageEnvelope Envelope,
    InventoryRestockedV1 Payload);
