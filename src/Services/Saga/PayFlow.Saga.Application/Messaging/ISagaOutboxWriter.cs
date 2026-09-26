namespace PayFlow.Saga.Application.Messaging;

public interface ISagaOutboxWriter
{
    Task AddAsync(
        OutgoingIntegrationMessage message,
        CancellationToken cancellationToken = default);
}
