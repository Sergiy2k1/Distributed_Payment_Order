namespace PayFlow.Saga.Application.Orders;

public sealed record BeginOrderProcessingV1(
    Guid OrderId);
