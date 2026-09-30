using PayFlow.MockPaymentProvider.Refund;

namespace PayFlow.MockPaymentProvider.UnitTests;

public sealed class MockRefundProviderStateTests
{
    [Fact]
    public void DefaultRefundSucceedsWithDeterministicReference()
    {
        var state =
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();
        var request =
            CreateRequest(refundId);

        var actual =
            state.Refund(
                "refund-key-001",
                request);

        Assert.Equal(
            MockRefundOutcome.Succeeded,
            actual.Outcome);
        Assert.Equal(
            $"mock-refund-{refundId:N}",
            actual.ProviderReference);
        Assert.Equal(1, actual.AttemptNumber);
        Assert.False(actual.IsIdempotentReplay);
    }

    [Fact]
    public void ConfiguredRejectReturnsDefinitiveRejectDecision()
    {
        var state =
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();

        state.ConfigureScenario(
            refundId,
            MockRefundScenario.Reject);

        var actual =
            state.Refund(
                "refund-key-reject",
                CreateRequest(refundId));

        Assert.Equal(
            MockRefundOutcome.Rejected,
            actual.Outcome);
        Assert.Null(actual.ProviderReference);
    }

    [Fact]
    public void TimeoutBeforeProcessingSucceedsOnSecondAttempt()
    {
        var state =
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();
        var request =
            CreateRequest(refundId);

        state.ConfigureScenario(
            refundId,
            MockRefundScenario.TimeoutBeforeProcessing);

        var first =
            state.Refund(
                "refund-key-timeout-before",
                request);

        var second =
            state.Refund(
                "refund-key-timeout-before",
                request);

        Assert.Equal(
            MockRefundOutcome.TimeoutBeforeProcessing,
            first.Outcome);
        Assert.Null(first.ProviderReference);
        Assert.Equal(
            MockRefundOutcome.Succeeded,
            second.Outcome);
        Assert.Equal(2, second.AttemptNumber);
        Assert.False(second.IsIdempotentReplay);
    }

    [Fact]
    public void TimeoutAfterProcessingReplaysPersistedSuccess()
    {
        var state =
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();
        var request =
            CreateRequest(refundId);

        state.ConfigureScenario(
            refundId,
            MockRefundScenario.TimeoutAfterProcessing);

        var first =
            state.Refund(
                "refund-key-timeout-after",
                request);

        var replay =
            state.Refund(
                "refund-key-timeout-after",
                request);

        Assert.Equal(
            MockRefundOutcome.TimeoutAfterProcessing,
            first.Outcome);
        Assert.NotNull(first.ProviderReference);
        Assert.Equal(
            MockRefundOutcome.Succeeded,
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
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();
        var request =
            CreateRequest(refundId);

        state.ConfigureScenario(
            refundId,
            MockRefundScenario.ServerErrorThenSuccess);

        var first =
            state.Refund(
                "refund-key-500",
                request);

        var second =
            state.Refund(
                "refund-key-500",
                request);

        var replay =
            state.Refund(
                "refund-key-500",
                request);

        Assert.Equal(
            MockRefundOutcome.ServerError,
            first.Outcome);
        Assert.Equal(
            MockRefundOutcome.Succeeded,
            second.Outcome);
        Assert.False(second.IsIdempotentReplay);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(
            second.ProviderReference,
            replay.ProviderReference);
    }

    [Fact]
    public void SameIdempotencyKeyWithDifferentPayloadThrowsConflict()
    {
        var state =
            new MockRefundProviderState();
        var refundId =
            Guid.NewGuid();
        var request =
            CreateRequest(refundId);

        state.Refund(
            "refund-key-conflict",
            request);

        var conflicting =
            request with
            {
                Amount = 99m
            };

        Assert.Throws<
            PayFlow.MockPaymentProvider.Capture.IdempotencyKeyConflictException>(
                () =>
                    state.Refund(
                        "refund-key-conflict",
                        conflicting));
    }

    [Theory]
    [InlineData(
        "success",
        MockRefundScenario.Success)]
    [InlineData(
        "reject",
        MockRefundScenario.Reject)]
    [InlineData(
        "timeout_before_processing",
        MockRefundScenario.TimeoutBeforeProcessing)]
    [InlineData(
        "timeout_after_processing",
        MockRefundScenario.TimeoutAfterProcessing)]
    [InlineData(
        "500_then_success",
        MockRefundScenario.ServerErrorThenSuccess)]
    public void ScenarioParserAcceptsDocumentedNames(
        string value,
        MockRefundScenario expected)
    {
        Assert.True(
            MockRefundScenarioParser.TryParse(
                value,
                out var actual));
        Assert.Equal(expected, actual);
    }

    private static RefundPaymentRequest CreateRequest(
        Guid refundId)
    {
        return new RefundPaymentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            refundId,
            35m,
            "USD");
    }
}
