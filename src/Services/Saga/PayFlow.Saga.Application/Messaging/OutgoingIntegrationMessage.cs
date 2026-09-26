namespace PayFlow.Saga.Application.Messaging;

public sealed record OutgoingIntegrationMessage(
    IntegrationMessageEnvelope Envelope,
    string Destination,
    object Payload);
