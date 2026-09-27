using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentCapturedMessage(
    IntegrationMessageEnvelope Envelope,
    PaymentCapturedV1 Payload);
