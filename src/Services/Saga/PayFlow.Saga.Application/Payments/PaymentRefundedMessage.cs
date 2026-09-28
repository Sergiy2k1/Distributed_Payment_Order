using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentRefundedMessage(
    IntegrationMessageEnvelope Envelope,
    PaymentRefundedV1 Payload);
