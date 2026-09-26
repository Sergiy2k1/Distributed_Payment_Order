namespace PayFlow.Payment.Application.Events;

public sealed record PaymentCapturedV1(
    Guid OrderId,
    Guid PaymentId,
    string ProviderReference,
    DateTimeOffset CapturedAtUtc);
