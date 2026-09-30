using PayFlow.MockPaymentProvider.Capture;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<
    MockPaymentProviderState>();

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

app.MapPut(
    "/scenarios/payments/{paymentId:guid}",
    CapturePaymentEndpoints.ConfigureScenario);

app.MapDelete(
    "/scenarios/payments/{paymentId:guid}",
    CapturePaymentEndpoints.ResetScenario);

app.Run();

public partial class Program
{
}
