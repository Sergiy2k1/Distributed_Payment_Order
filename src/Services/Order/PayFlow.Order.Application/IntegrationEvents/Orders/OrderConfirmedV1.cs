namespace PayFlow.Order.Application.IntegrationEvents.Orders;

public sealed record OrderConfirmedV1(
    Guid OrderId,
    DateTimeOffset ConfirmedAtUtc);
