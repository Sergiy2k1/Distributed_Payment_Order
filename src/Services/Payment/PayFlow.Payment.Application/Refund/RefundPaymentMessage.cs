using PayFlow.Payment.Application.Messaging;

namespace PayFlow.Payment.Application.Refund;

public sealed record RefundPaymentMessage(
    IntegrationMessageEnvelope Envelope,
    RefundPaymentV1 Payload);
