using PayFlow.Saga.Application.Messaging;

namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentRefundRejectedMessage(
    IntegrationMessageEnvelope Envelope,
    PaymentRefundRejectedV1 Payload);
