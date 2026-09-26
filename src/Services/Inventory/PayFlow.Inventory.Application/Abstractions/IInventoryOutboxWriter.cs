namespace PayFlow.Inventory.Application.Abstractions;

public interface IInventoryOutboxWriter
{
    Task AddAsync(
        Guid orderId,
        Guid correlationId,
        Guid causationId,
        DateTimeOffset occurredAtUtc,
        string messageType,
        object payload,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
