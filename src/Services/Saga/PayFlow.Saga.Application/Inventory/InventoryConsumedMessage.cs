using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryConsumedMessage(
    IntegrationMessageEnvelope Envelope,
    InventoryConsumedV1 Payload);
