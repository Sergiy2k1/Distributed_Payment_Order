namespace PayFlow.Payment.Application.Events;

public sealed record PaymentReconciledV1(
    Guid OrderId,
    Guid PaymentId,
    Guid ReconciliationId,
    string PaymentStatus,
    string? ProviderOperationStatus,
    int? ProviderAttemptCount,
    DateTimeOffset? NextAttemptAtUtc,
    string? LastErrorCode,
    string? ProviderReference,
    DateTimeOffset ReconciledAtUtc);
