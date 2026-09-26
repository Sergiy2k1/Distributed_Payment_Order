using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReservedMessage(
    IntegrationMessageEnvelope Envelope,
    InventoryReservedV1 Payload);
