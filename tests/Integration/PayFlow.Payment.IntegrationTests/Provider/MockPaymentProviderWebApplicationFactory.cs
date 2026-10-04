using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PayFlow.Payment.IntegrationTests.Provider;

public sealed class MockPaymentProviderWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestWebhookSigningSecret =
        "payflow-mock-provider-test-webhook-signing-secret-2026";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "MockPaymentProvider:WebhookSigningSecret",
            TestWebhookSigningSecret);

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [
                            "MockPaymentProvider:TimeoutSimulationDelay"
                        ] = "00:00:00.200"
                    });
            });
    }
}
