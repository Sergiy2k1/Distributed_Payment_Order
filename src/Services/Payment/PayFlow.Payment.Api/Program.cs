using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PayFlow.Observability;
using PayFlow.Payment.Api.Webhooks;
using PayFlow.Payment.Infrastructure.Messaging.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence;

namespace PayFlow.Payment.Api;

public sealed class Program
{
    private const string ProviderWebhookRateLimitPolicyName =
        "provider-webhook";

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

        var webhookRateLimitSection =
            builder.Configuration.GetSection(
                "RateLimiting:ProviderWebhook");
        var webhookPermitLimit =
            webhookRateLimitSection.GetValue(
                "PermitLimit",
                120);
        var webhookWindow =
            webhookRateLimitSection.GetValue(
                "Window",
                TimeSpan.FromMinutes(1));

        if (webhookPermitLimit <= 0)
        {
            throw new InvalidOperationException(
                "RateLimiting:ProviderWebhook:PermitLimit must be greater than zero.");
        }

        if (webhookWindow <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "RateLimiting:ProviderWebhook:Window must be greater than zero.");
        }

        builder.Services.AddRateLimiter(
            options =>
            {
                options.RejectionStatusCode =
                    StatusCodes.Status429TooManyRequests;

                options.AddPolicy(
                    ProviderWebhookRateLimitPolicyName,
                    httpContext =>
                    {
                        var partitionKey =
                            httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown";

                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    AutoReplenishment = true,
                                    PermitLimit = webhookPermitLimit,
                                    QueueLimit = 0,
                                    Window = webhookWindow
                                });
                    });
            });

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

        app.UseRateLimiter();

        app.MapPost(
                "/provider/webhooks",
                ProviderWebhookEndpoints.ReceiveSignedAsync)
            .RequireRateLimiting(
                ProviderWebhookRateLimitPolicyName);

        app.Run();
    }
}
