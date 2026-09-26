using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Orders;

public sealed record OrderCreatedMessage(
    IntegrationMessageEnvelope Envelope,
    OrderCreatedV1 Payload);
