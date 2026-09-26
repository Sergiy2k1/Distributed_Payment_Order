namespace PayFlow.Saga.Infrastructure.Persistence.Entities;

public sealed class CheckoutSagaItemEntity
{
    public Guid OrderId { get; set; }

    public int Position { get; set; }

    public string SkuId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    public CheckoutSagaEntity Saga { get; set; } = null!;
}
