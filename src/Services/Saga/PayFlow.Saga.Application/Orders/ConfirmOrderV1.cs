namespace PayFlow.Saga.Application.Orders;

public sealed record ConfirmOrderV1(
    Guid OrderId);
