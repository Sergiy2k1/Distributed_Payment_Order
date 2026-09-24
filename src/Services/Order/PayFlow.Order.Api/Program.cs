using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Api.Endpoints.Orders.CreateOrder;
using PayFlow.Order.Api.Errors;
using PayFlow.Order.Api.HostedServices;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Infrastructure.Messaging.Kafka;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

var orderDatabaseConnectionString =
    builder.Configuration.GetConnectionString("OrderDatabase");

if (string.IsNullOrWhiteSpace(orderDatabaseConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'OrderDatabase' is not configured.");
}

var outboxPublisherSection =
    builder.Configuration.GetSection("OutboxPublisher");

var outboxPublisherOptions =
    new OutboxPublisherOptions(
        outboxPublisherSection.GetValue(
            "BatchSize",
            50),
        outboxPublisherSection.GetValue(
            "LeaseDuration",
            TimeSpan.FromSeconds(30)),
        outboxPublisherSection.GetValue(
            "BaseRetryDelay",
            TimeSpan.FromSeconds(5)),
        outboxPublisherSection.GetValue(
            "MaxRetryDelay",
            TimeSpan.FromMinutes(1)));

var outboxPublisherWorkerOptions =
    new OutboxPublisherWorkerOptions(
        outboxPublisherSection.GetValue(
            "Enabled",
            false),
        outboxPublisherSection.GetValue(
            "PollInterval",
            TimeSpan.FromSeconds(1)));

var kafkaSection =
    builder.Configuration.GetSection("Kafka");

var kafkaProducerOptions =
    new KafkaProducerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "ClientId")
            ?? "payflow-order",
        kafkaSection.GetValue(
            "MessageTimeout",
            TimeSpan.FromSeconds(10)));

if (kafkaProducerOptions.MessageTimeout
    >= outboxPublisherOptions.LeaseDuration)
{
    throw new InvalidOperationException(
        "Kafka MessageTimeout must be shorter than the Outbox publisher LeaseDuration.");
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<IdempotencyConflictExceptionHandler>();
builder.Services.AddExceptionHandler<DomainValidationExceptionHandler>();

builder.Services.AddDbContext<OrderDbContext>(
    options => options.UseNpgsql(orderDatabaseConnectionString));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<
    ICreateOrderIdempotencyRepository,
    CreateOrderIdempotencyRepository>();
builder.Services.AddScoped<IOutboxWriter, OrderOutboxWriter>();
builder.Services.AddScoped<
    IOutboxMessageRepository,
    OutboxMessageRepository>();
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddSingleton(outboxPublisherOptions);
builder.Services.AddSingleton(outboxPublisherWorkerOptions);
builder.Services.AddSingleton(kafkaProducerOptions);
builder.Services.AddSingleton<IProducer<string, string>>(
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
builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<CreateOrderHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapCreateOrderEndpoint();

app.Run();
