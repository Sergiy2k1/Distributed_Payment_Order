namespace PayFlow.Order.Application.IntegrationEvents.Orders;

public sealed record OrderCancelledV1(
    Guid OrderId,
    string ReasonCode,
    DateTimeOffset CancelledAtUtc);
