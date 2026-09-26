using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayFlow.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContextFactory
    : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PAYFLOW_PAYMENT_DB")
            ?? "Host=localhost;Port=5436;Database=payments_db;Username=payflow_payment;Password=payflow_payment";

        var options =
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new PaymentDbContext(options);
    }
}
