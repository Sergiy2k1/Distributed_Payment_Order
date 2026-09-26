namespace PayFlow.Payment.Infrastructure.Persistence.Entities;

public sealed class PaymentEntity
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CapturedAtUtc { get; set; }
    public string? FailureReasonCode { get; set; }
    public long Version { get; set; }
}
