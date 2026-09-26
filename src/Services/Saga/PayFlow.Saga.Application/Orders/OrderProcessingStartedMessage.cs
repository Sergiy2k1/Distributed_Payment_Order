using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Orders;

public sealed record OrderProcessingStartedMessage(
    IntegrationMessageEnvelope Envelope,
    OrderProcessingStartedV1 Payload);
