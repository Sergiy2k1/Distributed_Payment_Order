namespace PayFlow.Payment.Infrastructure.Persistence.Entities;

public sealed class ProviderOperationEntity
{
    public Guid ProviderOperationId { get; set; }
    public Guid BusinessOperationId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string ProviderIdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? ProviderReference { get; set; }
    public long Version { get; set; }
}
