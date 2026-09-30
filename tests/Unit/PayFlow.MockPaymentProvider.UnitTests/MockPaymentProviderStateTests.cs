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
            MockCaptureOutcome.Succeeded,
            actual.Outcome);
        Assert.Equal(
            $"mock-capture-{paymentId:N}",
            actual.ProviderReference);
        Assert.Equal(1, actual.AttemptNumber);
        Assert.False(actual.IsIdempotentReplay);
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
            MockCaptureOutcome.Declined,
            actual.Outcome);
        Assert.Null(actual.ProviderReference);
    }

    [Fact]
    public void TimeoutBeforeProcessingSucceedsOnSecondAttempt()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();

        state.ConfigureScenario(
            paymentId,
            MockPaymentScenario.TimeoutBeforeProcessing);

        var request =
            CreateRequest(paymentId);

        var first =
            state.Capture(
                "capture-key-timeout-before",
                request);

        var second =
            state.Capture(
                "capture-key-timeout-before",
                request);

        Assert.Equal(
            MockCaptureOutcome.TimeoutBeforeProcessing,
            first.Outcome);
        Assert.Null(first.ProviderReference);
        Assert.Equal(1, first.AttemptNumber);
        Assert.Equal(
            MockCaptureOutcome.Succeeded,
            second.Outcome);
        Assert.Equal(2, second.AttemptNumber);
        Assert.False(second.IsIdempotentReplay);
    }

    [Fact]
    public void TimeoutAfterProcessingReplaysPersistedSuccess()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();
        var request =
            CreateRequest(paymentId);

        state.ConfigureScenario(
            paymentId,
            MockPaymentScenario.TimeoutAfterProcessing);

        var first =
            state.Capture(
                "capture-key-timeout-after",
                request);

        var replay =
            state.Capture(
                "capture-key-timeout-after",
                request);

        Assert.Equal(
            MockCaptureOutcome.TimeoutAfterProcessing,
            first.Outcome);
        Assert.NotNull(first.ProviderReference);
        Assert.Equal(
            MockCaptureOutcome.Succeeded,
            replay.Outcome);
        Assert.Equal(
            first.ProviderReference,
            replay.ProviderReference);
        Assert.True(replay.IsIdempotentReplay);
    }

    [Fact]
    public void ServerErrorThenSuccessFailsOnlyFirstAttempt()
    {
        var state =
            new MockPaymentProviderState();
        var paymentId =
            Guid.NewGuid();
        var request =
            CreateRequest(paymentId);

        state.ConfigureScenario(
            paymentId,
            MockPaymentScenario.ServerErrorThenSuccess);

        var first =
            state.Capture(
                "capture-key-500",
                request);

        var second =
            state.Capture(
                "capture-key-500",
                request);

        var replay =
            state.Capture(
                "capture-key-500",
                request);

        Assert.Equal(
            MockCaptureOutcome.ServerError,
            first.Outcome);
        Assert.Equal(
            MockCaptureOutcome.Succeeded,
            second.Outcome);
        Assert.False(second.IsIdempotentReplay);
        Assert.Equal(
            MockCaptureOutcome.Succeeded,
            replay.Outcome);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(
            second.ProviderReference,
            replay.ProviderReference);
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

        Assert.Equal(first.Outcome, replay.Outcome);
        Assert.Equal(
            first.ProviderReference,
            replay.ProviderReference);
        Assert.True(replay.IsIdempotentReplay);
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

    [Theory]
    [InlineData(
        "timeout_before_processing",
        MockPaymentScenario.TimeoutBeforeProcessing)]
    [InlineData(
        "timeout_after_processing",
        MockPaymentScenario.TimeoutAfterProcessing)]
    [InlineData(
        "500_then_success",
        MockPaymentScenario.ServerErrorThenSuccess)]
    public void ScenarioParserAcceptsDocumentedNames(
        string value,
        MockPaymentScenario expected)
    {
        Assert.True(
            MockPaymentScenarioParser.TryParse(
                value,
                out var actual));
        Assert.Equal(expected, actual);
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
