namespace PayFlow.Order.Infrastructure.Persistence.Entities;

public sealed class CreateOrderIdempotencyEntity
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public string ResponseStatus { get; set; } = string.Empty;

    public decimal ResponseTotalAmount { get; set; }

    public string ResponseCurrency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
