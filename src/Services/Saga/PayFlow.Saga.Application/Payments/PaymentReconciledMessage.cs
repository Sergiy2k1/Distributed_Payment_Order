using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentReconciledMessage(
    IntegrationMessageEnvelope Envelope,
    PaymentReconciledV1 Payload);
