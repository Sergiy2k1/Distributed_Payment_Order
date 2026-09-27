using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Orders;

public sealed record OrderConfirmedMessage(
    IntegrationMessageEnvelope Envelope,
    OrderConfirmedV1 Payload);
