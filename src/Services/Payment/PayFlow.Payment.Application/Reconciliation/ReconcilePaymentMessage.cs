using PayFlow.Payment.Application.Messaging;

namespace PayFlow.Payment.Application.Reconciliation;

public sealed record ReconcilePaymentMessage(
    IntegrationMessageEnvelope Envelope,
    ReconcilePaymentV1 Payload);
