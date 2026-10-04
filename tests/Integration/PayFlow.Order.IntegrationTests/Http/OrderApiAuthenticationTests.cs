using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
        builder.UseSetting(
            "ConnectionStrings:OrderDatabase",
            "Host=localhost;Database=payflow_auth_tests;Username=unused;Password=unused");
        builder.UseSetting(
            "Authentication:Enabled",
            "true");
        builder.UseSetting(
            "Authentication:JwtBearer:Authority",
            "https://identity.test");
        builder.UseSetting(
            "Authentication:JwtBearer:Audience",
            "payflow-order-api");
        builder.UseSetting(
            "Authentication:JwtBearer:RequireHttpsMetadata",
            "false");
        builder.UseSetting(
            "OutboxPublisher:Enabled",
            "false");
        builder.UseSetting(
            "Kafka:BootstrapServers",
            "localhost:65535");

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
                                TestAuthenticationHandler.AuthenticationSchemeName;
                            options.DefaultAuthenticateScheme =
                                TestAuthenticationHandler.AuthenticationSchemeName;
                            options.DefaultChallengeScheme =
                                TestAuthenticationHandler.AuthenticationSchemeName;
                        })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                        TestAuthenticationHandler.AuthenticationSchemeName,
                        _ => { });
            });
    }
}

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationSchemeName = "Test";
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
            AuthenticationSchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(
            principal,
            AuthenticationSchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
