using PayFlow.Payment.Application.Provider;

namespace PayFlow.Payment.Application.Abstractions;

public interface IProviderOperationExecutionRepository
{
    Task<ProviderCaptureWorkItem?> ClaimNextCaptureAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset staleProcessingBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<ProviderRefundWorkItem?> ClaimNextRefundAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset staleProcessingBeforeUtc,
        CancellationToken cancellationToken = default);
}
