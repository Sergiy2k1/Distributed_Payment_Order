namespace PayFlow.Saga.Infrastructure.Persistence.Entities;

public sealed class CheckoutSagaEntity
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset DeadlineAtUtc { get; set; }
    public Guid? ReservationId { get; set; }
    public DateTimeOffset? ReservationExpiresAtUtc { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? LastTechnicalErrorCode { get; set; }
    public string? LastTechnicalErrorMessage { get; set; }
    public long Version { get; set; }
    public List<CheckoutSagaItemEntity> Items { get; set; } = [];
}
