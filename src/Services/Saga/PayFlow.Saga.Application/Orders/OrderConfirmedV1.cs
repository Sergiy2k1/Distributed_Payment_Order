namespace PayFlow.Saga.Application.Orders;

public sealed record OrderConfirmedV1(
    Guid OrderId,
    DateTimeOffset ConfirmedAtUtc);
