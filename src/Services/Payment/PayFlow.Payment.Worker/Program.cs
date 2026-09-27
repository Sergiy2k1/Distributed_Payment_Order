using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Infrastructure.Messaging.Outbox;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.Infrastructure.Provider;
using PayFlow.Payment.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "PaymentDatabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'PaymentDatabase' is not configured.");
}

var executorSection =
    builder.Configuration.GetSection(
        "ProviderExecutor");

var executorOptions =
    new ProviderCaptureExecutorOptions(
        executorSection.GetValue(
            "StaleProcessingAfter",
            TimeSpan.FromMinutes(2)),
        executorSection.GetValue(
            "AmbiguousRetryDelay",
            TimeSpan.FromSeconds(30)));

var workerOptions =
    new ProviderCaptureWorkerOptions(
        executorSection.GetValue(
            "Enabled",
            false),
        executorSection.GetValue(
            "PollInterval",
            TimeSpan.FromSeconds(1)));

var providerSection =
    builder.Configuration.GetSection(
        "PaymentProvider");

var providerBaseAddress =
    providerSection.GetValue<string>(
        "BaseAddress");

if (!Uri.TryCreate(
        providerBaseAddress,
        UriKind.Absolute,
        out var providerUri))
{
    throw new InvalidOperationException(
        "PaymentProvider:BaseAddress must be an absolute URI.");
}

var providerOptions =
    new HttpPaymentProviderOptions(
        providerUri,
        providerSection.GetValue(
            "Timeout",
            TimeSpan.FromSeconds(10)));

builder.Services.AddDbContext<PaymentDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services.AddScoped<
    IPaymentRepository,
    PaymentRepository>();
builder.Services.AddScoped<
    IProviderOperationRepository,
    ProviderOperationRepository>();
builder.Services.AddScoped<
    IProviderOperationExecutionRepository,
    ProviderOperationExecutionRepository>();
builder.Services.AddScoped<
    ILedgerRepository,
    LedgerRepository>();
builder.Services.AddScoped<
    IPaymentUnitOfWork,
    PaymentEfUnitOfWork>();
builder.Services.AddScoped<
    IPaymentOutboxWriter,
    PaymentOutboxWriter>();
builder.Services.AddScoped<
    IProviderCaptureOutcomeFinalizer,
    ProviderCaptureOutcomeFinalizer>();
builder.Services.AddSingleton(
    executorOptions);
builder.Services.AddSingleton(
    workerOptions);
builder.Services.AddSingleton(
    providerOptions);
builder.Services.AddSingleton(
    TimeProvider.System);

builder.Services.AddHttpClient<
        IPaymentProvider,
        HttpPaymentProvider>(
        client =>
        {
            client.BaseAddress =
                providerOptions.BaseAddress;
            client.Timeout =
                providerOptions.Timeout;
        });

builder.Services.AddHostedService<
    ProviderCaptureBackgroundService>();

var host = builder.Build();

await host.RunAsync();
