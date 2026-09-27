namespace PayFlow.Payment.Application.Events;

public sealed record PaymentCapturedV1(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    DateTimeOffset CapturedAtUtc,
    string ProviderReference);
