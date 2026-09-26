using PayFlow.Payment.Application.Messaging;

namespace PayFlow.Payment.Application.Capture;

public sealed record CapturePaymentMessage(
    IntegrationMessageEnvelope Envelope,
    CapturePaymentV1 Payload);
