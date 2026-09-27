namespace PayFlow.Saga.Application.Payments;

public sealed record PaymentCapturedV1(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    DateTimeOffset CapturedAtUtc,
    string? ProviderReference);
