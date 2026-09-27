using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentFailedMessage(
    IntegrationMessageEnvelope Envelope,
    PaymentFailedV1 Payload);
