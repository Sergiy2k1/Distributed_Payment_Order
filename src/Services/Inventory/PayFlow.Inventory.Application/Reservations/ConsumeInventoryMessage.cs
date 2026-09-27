using PayFlow.Inventory.Application.Messaging;

namespace PayFlow.Inventory.Application.Reservations;

public sealed record ConsumeInventoryMessage(
    IntegrationMessageEnvelope Envelope,
    ConsumeInventoryV1 Payload);
