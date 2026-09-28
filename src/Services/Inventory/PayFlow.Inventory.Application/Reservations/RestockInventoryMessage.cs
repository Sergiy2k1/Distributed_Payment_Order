using PayFlow.Inventory.Application.Messaging;

namespace PayFlow.Inventory.Application.Reservations;

public sealed record RestockInventoryMessage(
    IntegrationMessageEnvelope Envelope,
    RestockInventoryV1 Payload);
