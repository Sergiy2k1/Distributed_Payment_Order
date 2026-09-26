using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Application.Reservations;
using PayFlow.Inventory.Infrastructure.Messaging;
using PayFlow.Inventory.Infrastructure.Messaging.Kafka;
using PayFlow.Inventory.Infrastructure.Messaging.Outbox;
using PayFlow.Inventory.Infrastructure.Persistence;
using PayFlow.Inventory.Infrastructure.Persistence.Repositories;
using PayFlow.Inventory.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "InventoryDatabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'InventoryDatabase' is not configured.");
}

var kafkaSection =
    builder.Configuration.GetSection("Kafka");

var workerOptions =
    new InventoryWorkerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "ConsumerGroup")
            ?? ReserveInventoryInboxProcessor.ConsumerName,
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
        workerOptions.BootstrapServers,
        kafkaSection.GetValue<string>(
            "ClientId")
            ?? "payflow-inventory",
        kafkaSection.GetValue(
            "MessageTimeout",
            TimeSpan.FromSeconds(10)));

if (kafkaProducerOptions.MessageTimeout
    >= outboxPublisherOptions.LeaseDuration)
{
    throw new InvalidOperationException(
        "Kafka MessageTimeout must be shorter than Inventory Outbox LeaseDuration.");
}

builder.Services.AddDbContext<InventoryDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services.AddScoped<
    IInventoryReservationRepository,
    InventoryReservationRepository>();
builder.Services.AddScoped<
    IStockRepository,
    StockRepository>();
builder.Services.AddScoped<
    IInventoryUnitOfWork,
    InventoryEfUnitOfWork>();
builder.Services.AddScoped<
    IInventoryOutboxWriter,
    InventoryOutboxWriter>();
builder.Services.AddScoped<
    IInboxMessageRepository,
    InboxMessageRepository>();
builder.Services.AddScoped<
    IReserveInventoryMessageHandler,
    ReserveInventoryMessageHandler>();
builder.Services.AddScoped<
    ReserveInventoryInboxProcessor>();

builder.Services.AddScoped<
    IOutboxMessageRepository,
    OutboxMessageRepository>();
builder.Services.AddScoped<OutboxPublisher>();

builder.Services.AddSingleton(
    workerOptions);
builder.Services.AddSingleton(
    outboxPublisherOptions);
builder.Services.AddSingleton(
    outboxWorkerOptions);
builder.Services.AddSingleton(
    TimeProvider.System);
builder.Services.AddSingleton(
    kafkaProducerOptions);

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

builder.Services.AddHostedService<
    ReserveInventoryConsumerBackgroundService>();
builder.Services.AddHostedService<
    OutboxPublisherBackgroundService>();

var host = builder.Build();

await host.RunAsync();
