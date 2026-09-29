namespace PayFlow.Saga.Application.Checkout;

public sealed record CheckoutSagaTimeoutOutcome(
    Guid OrderId,
    CheckoutSagaTimeoutAction Action);
