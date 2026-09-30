using PayFlow.MockPaymentProvider.Capture;

namespace PayFlow.MockPaymentProvider.UnitTests;

public sealed class MockPaymentProviderStateTests
{
    [Fact]
    public void DefaultCaptureSucceedsWithDeterministicReference()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();

        var actual =
            state.Capture(
                "capture-key-001",
                CreateRequest(paymentId));

        Assert.Equal(
            MockPaymentScenario.Success,
            actual.Scenario);
        Assert.Equal(
            $"mock-capture-{paymentId:N}",
            actual.ProviderReference);
        Assert.False(
            actual.IsIdempotentReplay);
    }

    [Fact]
    public void ConfiguredDeclineReturnsDefinitiveDeclineDecision()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();

        state.ConfigureScenario(
            paymentId,
            MockPaymentScenario.Decline);

        var actual =
            state.Capture(
                "capture-key-002",
                CreateRequest(paymentId));

        Assert.Equal(
            MockPaymentScenario.Decline,
            actual.Scenario);
        Assert.Null(
            actual.ProviderReference);
    }

    [Fact]
    public void SameIdempotencyKeyAndPayloadReplaysOriginalDecision()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();
        var request =
            CreateRequest(paymentId);

        var first =
            state.Capture(
                "capture-key-003",
                request);

        state.ConfigureScenario(
            paymentId,
            MockPaymentScenario.Decline);

        var replay =
            state.Capture(
                "capture-key-003",
                request);

        Assert.Equal(
            first.Scenario,
            replay.Scenario);
        Assert.Equal(
            first.ProviderReference,
            replay.ProviderReference);
        Assert.True(
            replay.IsIdempotentReplay);
    }

    [Fact]
    public void SameIdempotencyKeyWithDifferentPayloadThrowsConflict()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();

        state.Capture(
            "capture-key-004",
            CreateRequest(paymentId));

        var conflicting =
            CreateRequest(paymentId)
            with
            {
                Amount = 99m
            };

        Assert.Throws<
            IdempotencyKeyConflictException>(
                () =>
                    state.Capture(
                        "capture-key-004",
                        conflicting));
    }

    private static CapturePaymentRequest CreateRequest(
        Guid paymentId)
    {
        return new CapturePaymentRequest(
            paymentId,
            Guid.NewGuid(),
            35m,
            "USD");
    }
}
