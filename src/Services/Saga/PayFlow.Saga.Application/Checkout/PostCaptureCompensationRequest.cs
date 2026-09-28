namespace PayFlow.Saga.Application.Checkout;

public sealed record PostCaptureCompensationRequest(
    Guid OrderId,
    Guid RefundId,
    Guid CorrelationId,
    Guid CausationId,
    DateTimeOffset OccurredAtUtc,
    string ReasonCode,
    string? TraceParent);
