using PayFlow.Order.Application.Messaging;

namespace PayFlow.Order.Application.Orders.BeginOrderProcessing;

public sealed record BeginOrderProcessingMessage(
    IntegrationMessageEnvelope Envelope,
    BeginOrderProcessingV1 Payload);
