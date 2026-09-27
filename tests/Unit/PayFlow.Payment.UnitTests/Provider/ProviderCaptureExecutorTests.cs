using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Application.Provider;

namespace PayFlow.Payment.UnitTests.Provider;

public sealed class ProviderCaptureExecutorTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteNextUsesPersistedIdempotencyAndIntegrationContext()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var repository =
            new FakeExecutionRepository(
                new ProviderCaptureWorkItem(
                    Guid.NewGuid(),
                    paymentId,
                    orderId,
                    35m,
                    "USD",
                    $"payment:{paymentId:D}:capture:v1",
                    1,
                    correlationId,
                    causationId,
                    "00-test-trace"));

        var provider = new FakeProvider(
            PaymentProviderCaptureResult.Succeeded(
                "provider-ref-001"));

        var finalizer = new FakeFinalizer();

        var executor =
            new ProviderCaptureExecutor(
                repository,
                provider,
                finalizer,
                new FixedTimeProvider(NowUtc),
                new ProviderCaptureExecutorOptions(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromSeconds(30)));

        Assert.True(
            await executor.ExecuteNextAsync(
                cancellationToken));

        Assert.NotNull(provider.Request);
        Assert.Equal(
            $"payment:{paymentId:D}:capture:v1",
            provider.Request.IdempotencyKey);

        Assert.NotNull(finalizer.Context);
        Assert.Equal(correlationId, finalizer.Context.CorrelationId);
        Assert.Equal(causationId, finalizer.Context.CausationId);
        Assert.Equal("00-test-trace", finalizer.Context.TraceParent);
        Assert.Equal(
            NowUtc.AddSeconds(30),
            finalizer.NextAttemptAtUtc);
    }

    [Fact]
    public async Task ExecuteNextReturnsFalseWhenNoWorkIsAvailable()
    {
        var executor =
            new ProviderCaptureExecutor(
                new FakeExecutionRepository(null),
                new FakeProvider(
                    PaymentProviderCaptureResult.Succeeded(
                        "unused")),
                new FakeFinalizer(),
                new FixedTimeProvider(NowUtc),
                new ProviderCaptureExecutorOptions(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromSeconds(30)));

        Assert.False(
            await executor.ExecuteNextAsync(
                TestContext.Current.CancellationToken));
    }

    private sealed class FakeExecutionRepository(
        ProviderCaptureWorkItem? workItem)
        : IProviderOperationExecutionRepository
    {
        public Task<ProviderCaptureWorkItem?> ClaimNextCaptureAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset staleProcessingBeforeUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workItem);
        }
    }

    private sealed class FakeProvider(
        PaymentProviderCaptureResult result)
        : IPaymentProvider
    {
        public PaymentProviderCaptureRequest? Request { get; private set; }

        public Task<PaymentProviderCaptureResult> CaptureAsync(
            PaymentProviderCaptureRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeFinalizer
        : IProviderCaptureOutcomeFinalizer
    {
        public ProviderCaptureCompletionContext? Context { get; private set; }
        public DateTimeOffset? NextAttemptAtUtc { get; private set; }

        public Task FinalizeAsync(
            ProviderCaptureCompletionContext context,
            PaymentProviderCaptureResult result,
            DateTimeOffset occurredAtUtc,
            DateTimeOffset nextAttemptAtUtc,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            NextAttemptAtUtc = nextAttemptAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
