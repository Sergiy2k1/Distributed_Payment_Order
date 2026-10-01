using PayFlow.MockPaymentProvider;
using PayFlow.MockPaymentProvider.Capture;
using PayFlow.MockPaymentProvider.Refund;
using PayFlow.MockPaymentProvider.Webhooks;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MockPaymentProviderState>();
builder.Services.AddSingleton<MockRefundProviderState>();
builder.Services.AddSingleton<MockWebhookScenarioState>();
builder.Services.AddSingleton<MockWebhookDeliveryChannel>();
builder.Services.AddSingleton<MockWebhookDispatcher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient();
builder.Services.AddHostedService<MockWebhookDeliveryBackgroundService>();

var providerSection =
    builder.Configuration.GetSection("MockPaymentProvider");

builder.Services.AddSingleton(
    new MockPaymentProviderOptions(
        providerSection.GetValue(
            "TimeoutSimulationDelay",
            TimeSpan.FromSeconds(15)),
        new Uri(
            providerSection.GetValue(
                "PaymentWebhookEndpoint",
                "http://localhost:8086/provider/webhooks")!),
        providerSection.GetValue(
            "DelayedWebhookDelay",
            TimeSpan.FromSeconds(5)),
        providerSection.GetValue(
            "DuplicateWebhookDelay",
            TimeSpan.FromMilliseconds(100))));

var app = builder.Build();

app.MapGet(
    "/health",
    static () =>
        Results.Ok(new { status = "ok" }));

app.MapPost(
    "/payments/capture",
    CapturePaymentEndpoints.CaptureAsync);

app.MapPost(
    "/payments/refund",
    RefundPaymentEndpoints.RefundAsync);

app.MapPut(
    "/scenarios/payments/{paymentId:guid}",
    CapturePaymentEndpoints.ConfigureScenario);

app.MapDelete(
    "/scenarios/payments/{paymentId:guid}",
    CapturePaymentEndpoints.ResetScenario);

app.MapPut(
    "/scenarios/refunds/{refundId:guid}",
    RefundPaymentEndpoints.ConfigureScenario);

app.MapDelete(
    "/scenarios/refunds/{refundId:guid}",
    RefundPaymentEndpoints.ResetScenario);

app.MapPut(
    "/scenarios/payments/{paymentId:guid}/webhook",
    MockWebhookScenarioEndpoints.ConfigureCapture);

app.MapDelete(
    "/scenarios/payments/{paymentId:guid}/webhook",
    MockWebhookScenarioEndpoints.ResetCapture);

app.MapPut(
    "/scenarios/refunds/{refundId:guid}/webhook",
    MockWebhookScenarioEndpoints.ConfigureRefund);

app.MapDelete(
    "/scenarios/refunds/{refundId:guid}/webhook",
    MockWebhookScenarioEndpoints.ResetRefund);

app.Run();

public partial class Program
{
}
