using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.Observability;
using PayFlow.Saga.Application.Abstractions;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Application.Inventory;
using PayFlow.Saga.Application.Messaging;
using PayFlow.Saga.Application.Orders;
using PayFlow.Saga.Application.Payments;
using PayFlow.Saga.Infrastructure.Messaging;
using PayFlow.Saga.Infrastructure.Messaging.Kafka;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPayFlowObservability(
    builder.Configuration,
    "payflow-saga-worker",
    includeAspNetCoreInstrumentation: false);

var sagaDatabaseConnectionString =
    builder.Configuration.GetConnectionString("SagaDatabase");

if (string.IsNullOrWhiteSpace(sagaDatabaseConnectionString))
{
    throw new InvalidOperationException(
        "Connection string 'SagaDatabase' is not configured.");
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

var sagaRecoverySection =
    builder.Configuration.GetSection(
        "SagaRecovery");

var sagaRecoveryWorkerOptions =
    new SagaRecoveryWorkerOptions(
        sagaRecoverySection.GetValue(
            "Enabled",
            true),
        sagaRecoverySection.GetValue(
            "PollInterval",
            TimeSpan.FromSeconds(10)),
        sagaRecoverySection.GetValue(
            "BatchSize",
            50));

var kafkaSection =
    builder.Configuration.GetSection("Kafka");

var orderCreatedConsumerOptions =
    new OrderCreatedConsumerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "OrderCreatedConsumerGroup")
            ?? OrderCreatedInboxProcessor.ConsumerName,
        kafkaSection.GetValue(
            "ConsumeErrorDelay",
            TimeSpan.FromSeconds(1)),
        kafkaSection.GetValue(
            "CheckoutTimeout",
            TimeSpan.FromMinutes(30)));

var kafkaProducerOptions =
    new KafkaProducerOptions(
        kafkaSection.GetValue<string>(
            "BootstrapServers")
            ?? "localhost:9092",
        kafkaSection.GetValue<string>(
            "ClientId")
            ?? "payflow-saga",
        kafkaSection.GetValue(
            "MessageTimeout",
            TimeSpan.FromSeconds(10)));

if (kafkaProducerOptions.MessageTimeout
    >= outboxPublisherOptions.LeaseDuration)
{
    throw new InvalidOperationException(
        "Kafka MessageTimeout must be shorter than the Saga Outbox publisher LeaseDuration.");
}

builder.Services.AddDbContext<SagaDbContext>(
    options =>
        options.UseNpgsql(
            sagaDatabaseConnectionString));

builder.Services.AddScoped<
    IOutboxMessageRepository,
    OutboxMessageRepository>();
builder.Services.AddScoped<
    IInboxMessageRepository,
    InboxMessageRepository>();
builder.Services.AddScoped<
    ICheckoutSagaRepository,
    CheckoutSagaRepository>();
builder.Services.AddScoped<
    ISagaUnitOfWork,
    SagaEfUnitOfWork>();
builder.Services.AddScoped<
    ISagaOutboxWriter,
    SagaOutboxWriter>();
builder.Services.AddScoped<IOrderCreatedMessageHandler>(
    serviceProvider =>
        new OrderCreatedMessageHandler(
            serviceProvider.GetRequiredService<
                ICheckoutSagaRepository>(),
            serviceProvider.GetRequiredService<
                ISagaUnitOfWork>(),
            serviceProvider.GetRequiredService<
                ISagaOutboxWriter>(),
            orderCreatedConsumerOptions.CheckoutTimeout));
builder.Services.AddScoped<
    IOrderProcessingStartedMessageHandler,
    OrderProcessingStartedMessageHandler>();
builder.Services.AddScoped<
    IOrderConfirmedMessageHandler,
    OrderConfirmedMessageHandler>();
builder.Services.AddScoped<
    IOrderCancelledMessageHandler,
    OrderCancelledMessageHandler>();
builder.Services.AddScoped<
    IInventoryReservedMessageHandler,
    InventoryReservedMessageHandler>();
builder.Services.AddScoped<
    IInventoryReservationRejectedMessageHandler,
    InventoryReservationRejectedMessageHandler>();
builder.Services.AddScoped<
    IInventoryConsumedMessageHandler,
    InventoryConsumedMessageHandler>();
builder.Services.AddScoped<
    IInventoryReleasedMessageHandler,
    InventoryReleasedMessageHandler>();
builder.Services.AddScoped<
    IInventoryRestockedMessageHandler,
    InventoryRestockedMessageHandler>();
builder.Services.AddScoped<
    IPaymentCapturedMessageHandler,
    PaymentCapturedMessageHandler>();
builder.Services.AddScoped<
    IPaymentFailedMessageHandler,
    PaymentFailedMessageHandler>();
builder.Services.AddScoped<
    IPaymentRefundedMessageHandler,
    PaymentRefundedMessageHandler>();
builder.Services.AddScoped<
    IPaymentRefundRejectedMessageHandler,
    PaymentRefundRejectedMessageHandler>();
builder.Services.AddScoped<
    IPaymentReconciledMessageHandler,
    PaymentReconciledMessageHandler>();
builder.Services.AddScoped<
    IPostCaptureCompensationStarter,
    PostCaptureCompensationStarter>();
builder.Services.AddScoped<ICheckoutSagaTimeoutProcessor>(
    serviceProvider =>
        new CheckoutSagaTimeoutProcessor(
            serviceProvider.GetRequiredService<
                ICheckoutSagaRepository>(),
            serviceProvider.GetRequiredService<
                IPostCaptureCompensationStarter>(),
            serviceProvider.GetRequiredService<
                ISagaOutboxWriter>(),
            serviceProvider.GetRequiredService<
                ISagaUnitOfWork>(),
            sagaRecoverySection.GetValue(
                "ReconciliationRetryDelay",
                TimeSpan.FromSeconds(30))));
builder.Services.AddScoped<OrderCreatedInboxProcessor>();
builder.Services.AddScoped<OrderProcessingStartedInboxProcessor>();
builder.Services.AddScoped<OrderConfirmedInboxProcessor>();
builder.Services.AddScoped<OrderCancelledInboxProcessor>();
builder.Services.AddScoped<InventoryReservedInboxProcessor>();
builder.Services.AddScoped<InventoryReservationRejectedInboxProcessor>();
builder.Services.AddScoped<InventoryConsumedInboxProcessor>();
builder.Services.AddScoped<InventoryReleasedInboxProcessor>();
builder.Services.AddScoped<InventoryRestockedInboxProcessor>();
builder.Services.AddScoped<PaymentCapturedInboxProcessor>();
builder.Services.AddScoped<PaymentFailedInboxProcessor>();
builder.Services.AddScoped<PaymentRefundedInboxProcessor>();
builder.Services.AddScoped<PaymentRefundRejectedInboxProcessor>();
builder.Services.AddScoped<PaymentReconciledInboxProcessor>();
builder.Services.AddScoped<OutboxPublisher>();

builder.Services.AddSingleton(outboxPublisherOptions);
builder.Services.AddSingleton(outboxPublisherWorkerOptions);
builder.Services.AddSingleton(sagaRecoveryWorkerOptions);
builder.Services.AddSingleton(orderCreatedConsumerOptions);
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

builder.Services.AddHostedService<
    OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<
    SagaRecoveryBackgroundService>();
builder.Services.AddHostedService<
    OrderCreatedConsumerBackgroundService>();

var host = builder.Build();

await host.RunAsync();
