using Microsoft.EntityFrameworkCore;
using PayFlow.Order.Api.Endpoints.Orders.CreateOrder;
using PayFlow.Order.Api.Errors;
using PayFlow.Order.Application.Abstractions;
using PayFlow.Order.Application.Orders.CreateOrder;
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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainValidationExceptionHandler>();

builder.Services.AddDbContext<OrderDbContext>(
    options => options.UseNpgsql(orderDatabaseConnectionString));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<CreateOrderHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapCreateOrderEndpoint();

app.Run();
