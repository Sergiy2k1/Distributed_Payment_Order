using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Api.Webhooks;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence;

namespace PayFlow.Payment.Api;

public sealed class Program
{
    public static void Main(
        string[] args)
    {
        var builder =
            WebApplication.CreateBuilder(args);

        var connectionString =
            builder.Configuration.GetConnectionString(
                "PaymentDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'PaymentDatabase' is not configured.");
        }

        builder.Services.AddDbContext<PaymentDbContext>(
            options =>
                options.UseNpgsql(
                    connectionString));

        builder.Services.AddScoped<
            ProviderWebhookInboxRepository>();

        builder.Services.AddSingleton(
            TimeProvider.System);

        var app =
            builder.Build();

        app.MapPost(
            "/provider/webhooks",
            ProviderWebhookEndpoints.ReceiveAsync);

        app.Run();
    }
}
