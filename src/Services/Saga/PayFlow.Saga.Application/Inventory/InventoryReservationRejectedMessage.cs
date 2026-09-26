using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Inventory;

public sealed record InventoryReservationRejectedMessage(
    IntegrationMessageEnvelope Envelope,
    InventoryReservationRejectedV1 Payload);
