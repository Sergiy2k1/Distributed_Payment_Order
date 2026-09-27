namespace PayFlow.Saga.Application.Orders;

public sealed record OrderCancelledV1(
    Guid OrderId,
    string ReasonCode,
    DateTimeOffset CancelledAtUtc);
