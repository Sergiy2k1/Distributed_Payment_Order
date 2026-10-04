using Microsoft.EntityFrameworkCore;
using PayFlow.Observability;
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

        builder.Services.AddPayFlowObservability(
            builder.Configuration,
            "payflow-payment-api",
            includeAspNetCoreInstrumentation: true);

        var connectionString =
            builder.Configuration.GetConnectionString(
                "PaymentDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'PaymentDatabase' is not configured.");
        }

        var signingSecret =
            builder.Configuration.GetValue<string>(
                "ProviderWebhook:SigningSecret");

        builder.Services.AddSingleton(
            new ProviderWebhookSigningOptions(
                signingSecret ?? string.Empty));

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
            ProviderWebhookEndpoints.ReceiveSignedAsync);

        app.Run();
    }
}
