using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Orders;

public sealed record OrderCancelledMessage(
    IntegrationMessageEnvelope Envelope,
    OrderCancelledV1 Payload);
