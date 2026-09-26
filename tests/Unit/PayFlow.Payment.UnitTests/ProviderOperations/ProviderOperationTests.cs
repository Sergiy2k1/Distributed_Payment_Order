using PayFlow.Payment.Domain.ProviderOperations;

namespace PayFlow.Payment.UnitTests.ProviderOperations;

public sealed class ProviderOperationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CaptureUsesDeterministicProviderIdempotencyKey()
    {
        var paymentId = Guid.NewGuid();

        var first = ProviderOperation.CreateCapture(
            Guid.NewGuid(),
            paymentId,
            CreatedAtUtc);

        var second = ProviderOperation.CreateCapture(
            Guid.NewGuid(),
            paymentId,
            CreatedAtUtc);

        Assert.Equal(
            $"payment:{paymentId:D}:capture:v1",
            first.ProviderIdempotencyKey);
        Assert.Equal(
            first.ProviderIdempotencyKey,
            second.ProviderIdempotencyKey);
    }

    [Fact]
    public void TimeoutLikeOutcomeRemainsAmbiguous()
    {
        var operation = ProviderOperation.CreateCapture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc);

        operation.BeginAttempt(
            CreatedAtUtc.AddSeconds(1));

        operation.MarkAmbiguous(
            "TIMEOUT",
            CreatedAtUtc.AddSeconds(2),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(
            ProviderOperationStatus.Ambiguous,
            operation.Status);
        Assert.Equal(1, operation.AttemptCount);
        Assert.Equal("TIMEOUT", operation.LastErrorCode);
    }

    [Fact]
    public void AmbiguousOperationCanLaterResolveToSuccess()
    {
        var operation = ProviderOperation.CreateCapture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc);

        operation.BeginAttempt(
            CreatedAtUtc.AddSeconds(1));
        operation.MarkAmbiguous(
            "TIMEOUT",
            CreatedAtUtc.AddSeconds(2),
            CreatedAtUtc.AddMinutes(1));
        operation.MarkSucceeded(
            "provider-ref-123",
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(
            ProviderOperationStatus.Succeeded,
            operation.Status);
        Assert.Equal(
            "provider-ref-123",
            operation.ProviderReference);
    }
}
