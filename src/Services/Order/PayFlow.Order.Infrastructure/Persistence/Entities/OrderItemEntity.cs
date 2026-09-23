namespace PayFlow.Order.Infrastructure.Persistence.Entities;

public sealed class OrderItemEntity
{
    public Guid OrderId { get; set; }

    public int Position { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPriceAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public OrderEntity Order { get; set; } = null!;
}
