using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Capture;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Infrastructure.Messaging;
using PayFlow.Payment.Infrastructure.Messaging.Kafka;
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

var providerWorkerOptions =
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

var kafkaSection =
    builder.Configuration.GetSection("Kafka");

var paymentWorkerOptions =
    new PaymentWorkerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "ConsumerGroup")
            ?? CapturePaymentInboxProcessor.ConsumerName,
        kafkaSection.GetValue(
            "ConsumeErrorDelay",
            TimeSpan.FromSeconds(1)));

var outboxSection =
    builder.Configuration.GetSection(
        "OutboxPublisher");

var outboxPublisherOptions =
    new OutboxPublisherOptions(
        outboxSection.GetValue(
            "BatchSize",
            50),
        outboxSection.GetValue(
            "LeaseDuration",
            TimeSpan.FromSeconds(30)),
        outboxSection.GetValue(
            "BaseRetryDelay",
            TimeSpan.FromSeconds(5)),
        outboxSection.GetValue(
            "MaxRetryDelay",
            TimeSpan.FromMinutes(1)));

var outboxWorkerOptions =
    new OutboxPublisherWorkerOptions(
        outboxSection.GetValue(
            "Enabled",
            false),
        outboxSection.GetValue(
            "PollInterval",
            TimeSpan.FromSeconds(1)));

var kafkaProducerOptions =
    new KafkaProducerOptions(
        paymentWorkerOptions.BootstrapServers,
        kafkaSection.GetValue<string>(
            "ClientId")
            ?? "payflow-payment",
        kafkaSection.GetValue(
            "MessageTimeout",
            TimeSpan.FromSeconds(10)));

if (kafkaProducerOptions.MessageTimeout
    >= outboxPublisherOptions.LeaseDuration)
{
    throw new InvalidOperationException(
        "Kafka MessageTimeout must be shorter than Payment Outbox LeaseDuration.");
}

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

builder.Services.AddScoped<
    ICapturePaymentMessageHandler,
    CapturePaymentMessageHandler>();
builder.Services.AddScoped<
    InboxMessageRepository>();
builder.Services.AddScoped<
    CapturePaymentInboxProcessor>();

builder.Services.AddScoped<
    IOutboxMessageRepository,
    OutboxMessageRepository>();
builder.Services.AddScoped<
    OutboxPublisher>();

builder.Services.AddSingleton(executorOptions);
builder.Services.AddSingleton(providerWorkerOptions);
builder.Services.AddSingleton(providerOptions);
builder.Services.AddSingleton(paymentWorkerOptions);
builder.Services.AddSingleton(outboxPublisherOptions);
builder.Services.AddSingleton(outboxWorkerOptions);
builder.Services.AddSingleton(kafkaProducerOptions);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<
    IProducer<string, string>>(
    _ => new ProducerBuilder<string, string>(
            KafkaProducerConfigFactory.Create(
                kafkaProducerOptions))
        .Build());

builder.Services.AddSingleton<
    IKafkaMessageProducer,
    ConfluentKafkaMessageProducer>();
builder.Services.AddSingleton<
    IOutboxTransport,
    KafkaOutboxTransport>();

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
    CapturePaymentConsumerBackgroundService>();
builder.Services.AddHostedService<
    OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<
    ProviderCaptureBackgroundService>();

var host = builder.Build();

await host.RunAsync();
