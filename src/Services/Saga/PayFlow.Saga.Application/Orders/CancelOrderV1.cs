namespace PayFlow.Saga.Application.Orders;

public sealed record CancelOrderV1(
    Guid OrderId,
    string ReasonCode);
