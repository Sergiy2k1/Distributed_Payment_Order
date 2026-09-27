using PayFlow.Order.Application.Messaging;

namespace PayFlow.Order.Application.Orders.CancelOrder;

public sealed record CancelOrderMessage(
    IntegrationMessageEnvelope Envelope,
    CancelOrderV1 Payload);
