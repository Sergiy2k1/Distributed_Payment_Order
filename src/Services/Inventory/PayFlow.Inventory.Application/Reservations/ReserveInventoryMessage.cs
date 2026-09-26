using PayFlow.Inventory.Application.Messaging;

namespace PayFlow.Inventory.Application.Reservations;

public sealed record ReserveInventoryMessage(
    IntegrationMessageEnvelope Envelope,
    ReserveInventoryV1 Payload);
