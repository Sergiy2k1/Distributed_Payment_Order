namespace PayFlow.Payment.Application.Abstractions;

public interface IPaymentOutboxWriter
{
    Task AddAsync(
        Guid orderId,
        Guid correlationId,
        Guid? causationId,
        DateTimeOffset occurredAtUtc,
        string messageType,
        object payload,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
