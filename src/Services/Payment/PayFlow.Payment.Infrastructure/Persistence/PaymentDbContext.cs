using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Infrastructure.Persistence.Entities;

namespace PayFlow.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(
        DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<PaymentEntity> Payments =>
        Set<PaymentEntity>();

    public DbSet<ProviderOperationEntity> ProviderOperations =>
        Set<ProviderOperationEntity>();

    public DbSet<LedgerTransactionEntity> LedgerTransactions =>
        Set<LedgerTransactionEntity>();

    public DbSet<LedgerEntryEntity> LedgerEntries =>
        Set<LedgerEntryEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentDbContext).Assembly);
    }
}
