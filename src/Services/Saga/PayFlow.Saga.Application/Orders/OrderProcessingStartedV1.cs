namespace PayFlow.Saga.Application.Orders;

public sealed record OrderProcessingStartedV1(
    Guid OrderId);
