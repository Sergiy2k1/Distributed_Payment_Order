namespace PayFlow.Order.Infrastructure.Persistence.Entities;

public sealed class OrderEntity
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long Version { get; set; }
}
