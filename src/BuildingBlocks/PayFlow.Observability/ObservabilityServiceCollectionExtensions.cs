using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PayFlow.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddPayFlowObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCoreInstrumentation)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var section =
            configuration.GetSection("OpenTelemetry");

        if (!bool.TryParse(
                section["Enabled"],
                out var enabled)
            || !enabled)
        {
            return services;
        }

        var endpoint =
            ParseEndpoint(section["OtlpEndpoint"]);

        var openTelemetry =
            services.AddOpenTelemetry()
                .ConfigureResource(
                    resource =>
                        resource.AddService(
                            serviceName: serviceName));

        openTelemetry.WithTracing(
            tracing =>
            {
                if (includeAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                tracing.AddHttpClientInstrumentation();

                if (endpoint is null)
                {
                    tracing.AddOtlpExporter();
                }
                else
                {
                    tracing.AddOtlpExporter(
                        options =>
                            options.Endpoint = endpoint);
                }
            });

        openTelemetry.WithMetrics(
            metrics =>
            {
                if (includeAspNetCoreInstrumentation)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddMeter("PayFlow.Saga");
                metrics.AddMeter(OutboxMetrics.MeterName);
                metrics.AddMeter(InboxMetrics.MeterName);

                if (endpoint is null)
                {
                    metrics.AddOtlpExporter();
                }
                else
                {
                    metrics.AddOtlpExporter(
                        options =>
                            options.Endpoint = endpoint);
                }
            });

        return services;
    }

    private static Uri? ParseEndpoint(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var endpoint))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:OtlpEndpoint must be an absolute URI.");
        }

        return endpoint;
    }
}
