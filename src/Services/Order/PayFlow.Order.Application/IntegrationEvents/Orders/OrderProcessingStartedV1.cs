namespace PayFlow.Order.Application.IntegrationEvents.Orders;

public sealed record OrderProcessingStartedV1(
    Guid OrderId);
