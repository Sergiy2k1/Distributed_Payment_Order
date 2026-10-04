using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PayFlow.Order.IntegrationTests.Http;

public sealed class OrderApiAuthenticationTests(
    OrderApiAuthenticationFactory factory)
    : IClassFixture<OrderApiAuthenticationFactory>
{
    [Fact]
    public async Task CreateOrderReturnsUnauthorizedWithoutAuthenticatedIdentity()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                CustomerId = Guid.Empty,
                Items = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateOrderReachesEndpointValidationWithAuthenticatedIdentity()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.AuthenticatedHeader,
            "true");

        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                CustomerId = Guid.Empty,
                Items = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}

public sealed class OrderApiAuthenticationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:OrderDatabase"] =
                            "Host=localhost;Database=payflow_auth_tests;Username=unused;Password=unused",
                        ["Authentication:Enabled"] = "true",
                        ["Authentication:JwtBearer:Authority"] =
                            "https://identity.test",
                        ["Authentication:JwtBearer:Audience"] =
                            "payflow-order-api",
                        ["Authentication:JwtBearer:RequireHttpsMetadata"] =
                            "false",
                        ["OutboxPublisher:Enabled"] = "false",
                        ["Kafka:BootstrapServers"] = "localhost:65535"
                    });
            });

        builder.ConfigureTestServices(
            services =>
            {
                var hostedServices = services
                    .Where(
                        static descriptor =>
                            descriptor.ServiceType
                                == typeof(IHostedService)
                            && descriptor.ImplementationType?
                                .Namespace
                                == "PayFlow.Order.Api.HostedServices")
                    .ToArray();

                foreach (var descriptor in hostedServices)
                {
                    services.Remove(descriptor);
                }

                services
                    .AddAuthentication(
                        options =>
                        {
                            options.DefaultScheme =
                                TestAuthenticationHandler.Scheme;
                            options.DefaultAuthenticateScheme =
                                TestAuthenticationHandler.Scheme;
                            options.DefaultChallengeScheme =
                                TestAuthenticationHandler.Scheme;
                        })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                        TestAuthenticationHandler.Scheme,
                        _ => { });
            });
    }
}

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Test";
    public const string AuthenticatedHeader =
        "X-Test-Authenticated";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                AuthenticatedHeader,
                out var headerValue)
            || !string.Equals(
                headerValue.ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "order-api-auth-test-user")
            ],
            Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(
            principal,
            Scheme);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
