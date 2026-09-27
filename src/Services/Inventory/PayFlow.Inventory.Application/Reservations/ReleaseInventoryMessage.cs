using PayFlow.Inventory.Application.Messaging;

namespace PayFlow.Inventory.Application.Reservations;

public sealed record ReleaseInventoryMessage(
    IntegrationMessageEnvelope Envelope,
    ReleaseInventoryV1 Payload);
