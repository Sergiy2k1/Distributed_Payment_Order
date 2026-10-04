using Microsoft.AspNetCore.Authentication.JwtBearer;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PayFlow.Observability;
using PayFlow.Order.Api.Endpoints.Orders.CreateOrder;
using PayFlow.Order.Api.Errors;
using PayFlow.Order.Api.HostedServices;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.BeginOrderProcessing;
using PayFlow.Order.Application.Orders.CreateOrder;
using PayFlow.Order.Application.Orders.ConfirmOrder;
using PayFlow.Order.Application.Orders.CancelOrder;
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
    include