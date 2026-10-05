using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Api.Webhooks;
using PayFlow.Payment.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Payment.IntegrationTests.Webhooks;

public sealed class ProviderWebhookSignatureHttpTests(
    PaymentApiWebhookSigningFactory factory)
    : IClassFixture<PaymentApiWebhookSigningFactory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidSignatureIsAccepted()
    {
        var payload =
            Serialize(
                CreateRequest(
                    providerReference: "provider-capture-valid"));

        using var request =
            CreateHttpRequest(
                payload,
                PaymentApiWebhookSigningFactory.CreateSignature(payload));
        using var client = factory.CreateClient();

        using var response =
            await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Accepted,
            response.StatusCode);
    }

    [Fact]
    public async Task MissingSignatureIsRejected()
    {
        var payload =
            Serialize(
                CreateRequest(
                    providerReference: "provider-capture-missing"));

        using var request =
            CreateHttpRequest(
                payload,
                signature: null);
        using var client = factory.CreateClient();

        using var response =
            await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignatureIsRejected()
    {
        var payload =
            Serialize(
                CreateRequest(
                    providerReference: "provider-capture-invalid"));

        using var request =
            CreateHttpRequest(
                payload,
                $"sha256={new string('0', 64)}");
        using var client = factory.CreateClient();

        using var response =
            await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task TamperedPayloadIsRejected()
    {
        var original =
            CreateRequest(
                providerReference: "provider-capture-original");
        var originalPayload =
            Serialize(original);
        var signature =
            PaymentApiWebhookSigningFactory.CreateSignature(
                originalPayload);

        var tamperedPayload =
            Serialize(
                original with
                {
                    ProviderReference =
                        "provider-capture-tampered"
                });

        using var request =
            CreateHttpRequest(
                tamperedPayload,
                signature);
        using var client = factory.CreateClient();

        using var response =
            await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private static byte[] Serialize(
        ProviderWebhookRequest request)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            request,
            JsonOptions);
    }

    private static HttpRequestMessage CreateHttpRequest(
        byte[] payload,
        string? signature)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/provider/webhooks")
            {
                Content = new ByteArrayContent(payload)
            };

        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/json");

        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(
                ProviderWebhookSignatureVerifier.HeaderName,
                signature);
        }

        return request;
    }

    private static ProviderWebhookRequest CreateRequest(
        string providerReference)
    {
        return new ProviderWebhookRequest(
            Guid.NewGuid(),
            "PaymentProviderResult.v1",
            "Capture",
            Guid.NewGuid(),
            null,
            "Succeeded",
            providerReference,
            null,
            new DateTimeOffset(
                2026,
                10,
                5,
                18,
                0,
                0,
                TimeSpan.Zero));
    }
}

public sealed class PaymentApiWebhookSigningFactory
    : WebApplicationFactory<PayFlow.Payment.Api.Program>,
        IAsyncLifetime
{
    public const string SigningSecret =
        "payflow-payment-webhook-test-secret-2026";

    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payments_webhook_auth_db")
            .WithUsername("payflow_payment")
            .WithPassword("payflow-test")
            .Build();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:PaymentDatabase",
            _container.GetConnectionString());
        builder.UseSetting(
            "ProviderWebhook:SigningSecret",
            SigningSecret);
    }

    public static string CreateSignature(
        byte[] payload)
    {
        using var hmac =
            new HMACSHA256(
                Encoding.UTF8.GetBytes(
                    SigningSecret));

        return $"sha256={Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant()}";
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await _container.StartAsync(
            cancellationToken);

        var options =
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseNpgsql(
                    _container.GetConnectionString())
                .Options;

        await using var dbContext =
            new PaymentDbContext(options);

        await dbContext.Database.MigrateAsync(
            cancellationToken);
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await _container.DisposeAsync();
    }
}
