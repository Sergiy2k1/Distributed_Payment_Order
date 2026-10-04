using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using PayFlow.Observability;
using PayFlow.Order.Api.Endpoints.Orders.CreateOrder;
using PayFlow.Order.Api.Errors;
using PayFlow.Order.Api.HostedServices;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.BeginOrderProcessing;
using PayFlow.Order.Application.Orders.CancelOrder;
using PayFlow.Order.Application.Orders.ConfirmOrder;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Infrastructure.Messaging;
using PayFlow.Order.Infrastructure.Messaging.Kafka;
using PayFlow.Order.Infrastructure.Messaging.Outbox;
using PayFlow.Order.Infrastructure.Persistence;
using PayFlow.Order.Infrastructure.Persistence.Repositories;
using PayFlow.Order.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPayFlowObservability(
    builder.Configuration,
    "payflow-order-api",
    includeAspNetCoreInstrumentation: true);

var authenticationSection =
    builder.Configuration.GetSection("Authentication");
var authenticationEnabled =
    authenticationSection.GetValue(
        "Enabled",
        false);

if (authenticationEnabled)
{
    var jwtBearerSection =
        authenticationSection.GetSection("JwtBearer");
    var authority =
        jwtBearerSection.GetValue<string>("Authority");
    var audience =
        jwtBearerSection.GetValue<string>("Audience");

    if (string.IsNullOrWhiteSpace(authority))
    {
        throw new InvalidOperationException(
            "Authentication:JwtBearer:Authority is required when authentication is enabled.");
    }

    if (string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException(
            "Authentication:JwtBearer:Audience is required when authentication is enabled.");
    }

    builder.Services
        .AddAuthentication(
            JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(
            options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata =
                    jwtBearerSection.GetValue(
                        "RequireHttpsMetadata",
                        true);
            });

    builder.Services.AddAuthorization();
}

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

var orderCommandsConsumerOptions =
    new OrderCommandsConsumerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "OrderCommandsConsumerGroup")
            ?? BeginOrderProcessingInboxProcessor.ConsumerName,
        kafkaSection.GetValue(
            "ConsumeErrorDelay",
            TimeSpan.FromSeconds(1)));

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
builder.Services.AddScoped<IOrderCommandRepository, OrderRepository>();
builder.Services.AddScoped<
    ICreateOrderIdempotencyRepository,
    CreateOrderIdempotencyRepository>();
builder.Services.AddScoped<
    IInboxMessageRepository,
    InboxMessageRepository>();
builder.Services.AddScoped<IOutboxWriter, OrderOutboxWriter>();
builder.Services.AddScoped<
    IOrderCommandOutboxWriter,
    OrderCommandOutboxWriter>();
builder.Services.AddScoped<
    ICancelOrderOutboxWriter,
    CancelOrderOutboxWriter>();
builder.Services.AddScoped<
    IOutboxMessageRepository,
    OutboxMessageRepository>();
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddSingleton(outboxPublisherOptions);
builder.Services.AddSingleton(outboxPublisherWorkerOptions);
builder.Services.AddSingleton(orderCommandsConsumerOptions);
builder.Services.AddSingleton(TimeProvider.System);
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
builder.Services.AddHostedService<OrderCommandsConsumerBackgroundService>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<
    IBeginOrderProcessingMessageHandler,
    BeginOrderProcessingMessageHandler>();
builder.Services.AddScoped<
    IConfirmOrderMessageHandler,
    ConfirmOrderMessageHandler>();
builder.Services.AddScoped<
    ICancelOrderMessageHandler,
    CancelOrderMessageHandler>();
builder.Services.AddScoped<BeginOrderProcessingInboxProcessor>();
builder.Services.AddScoped<ConfirmOrderInboxProcessor>();
builder.Services.AddScoped<CancelOrderInboxProcessor>();

var app = builder.Build();

app.UseExceptionHandler();

if (authenticationEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapCreateOrderEndpoint(
    authenticationEnabled);

app.Run();
