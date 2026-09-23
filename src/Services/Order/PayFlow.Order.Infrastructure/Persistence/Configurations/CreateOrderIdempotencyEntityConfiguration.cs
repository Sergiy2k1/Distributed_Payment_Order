using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Order.Infrastructure.Persistence.Entities;

namespace PayFlow.Order.Infrastructure.Persistence.Configurations;

public sealed class CreateOrderIdempotencyEntityConfiguration
    : IEntityTypeConfiguration<CreateOrderIdempotencyEntity>
{
    public void Configure(
        EntityTypeBuilder<CreateOrderIdempotencyEntity> builder)
    {
        builder.ToTable("order_idempotency");

        builder.HasKey(record => record.IdempotencyKey);

        builder.Property(record => record.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(record => record.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(record => record.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(record => record.ResponseStatus)
            .HasColumnName("response_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.ResponseTotalAmount)
            .HasColumnName("response_total_amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(record => record.ResponseCurrency)
            .HasColumnName("response_currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(record => record.OrderId)
            .IsUnique();

        builder.HasOne<OrderEntity>()
            .WithOne()
            .HasForeignKey<CreateOrderIdempotencyEntity>(
                record => record.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
