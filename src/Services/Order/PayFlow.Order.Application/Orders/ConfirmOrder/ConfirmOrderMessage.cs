using PayFlow.Order.Application.Messaging;

namespace PayFlow.Order.Application.Orders.ConfirmOrder;

public sealed record ConfirmOrderMessage(
    IntegrationMessageEnvelope Envelope,
    ConfirmOrderV1 Payload);
