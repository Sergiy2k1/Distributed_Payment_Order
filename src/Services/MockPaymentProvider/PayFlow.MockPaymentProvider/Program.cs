using PayFlow.MockPaymentProvider;
using PayFlow.MockPaymentProvider.Capture;
using PayFlow.MockPaymentProvider.Refund;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<
    MockPaymentProviderState>();
builder.Services.AddSingleton<
    MockRefundProviderState>();

var providerSection =
    builder.Configuration.GetSection(
        "MockPaymentProvider");

builder.Services.AddSingleton(
    new MockPaymentProviderOptions(
        providerSection.GetValue(
            "TimeoutSimulationDelay",
            TimeSpan.FromSeconds(15))));

var app = builder.Build();

app.MapGet(
    "/health",
    static () =>
        Results.Ok(
            new
            {
                status = "ok"
            }));

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

app.Run();

public partial class Program
{
}
