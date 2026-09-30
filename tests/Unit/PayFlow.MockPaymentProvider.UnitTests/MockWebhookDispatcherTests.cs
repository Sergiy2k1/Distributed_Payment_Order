using PayFlow.MockPaymentProvider.Capture;
using PayFlow.MockPaymentProvider.Refund;
using PayFlow.MockPaymentProvider.Webhooks;

namespace PayFlow.MockPaymentProvider.UnitTests;

public sealed class MockWebhookDispatcherTests
{
    [Fact]
    public async Task DuplicateWebhookSchedulesSameEventTwice()
    {
        var state = new MockWebhookScenarioState();
        var queue = new MockWebhookDeliveryQueue();
        var paymentId = Guid.NewGuid();
        var request = new CapturePaymentRequest(
            paymentId,
            Guid.NewGuid(),
            35m,
            "USD");

        state.ConfigureCapture(
            paymentId,
            MockWebhookScenario.DuplicateWebhook);

        var dispatcher = CreateDispatcher(
            state,
            queue);

        await dispatcher.ScheduleCaptureAsync(
            request,
            new MockCaptureDecision(
                MockPaymentScenario.Success,
                MockCaptureOutcome.Succeeded,
                $"mock-capture-{paymentId:N}",
                1,
                false),
            TestContext.Current.CancellationToken);

        Assert.True(queue.TryRead(out var first));
        Assert.True(queue.TryRead(out var second));
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(
            first.Payload.EventId,
            second.Payload.EventId);
        Assert.Equal(
            TimeSpan.Zero,
            first.Delay);
        Assert.Equal(
            TimeSpan.FromMilliseconds(50),
            second.Delay);
        Assert.False(
            queue.TryRead(out _));
    }

    [Fact]
    public async Task DelayedRefundSchedulesSingleDelayedWebhook()
    {
        var state = new MockWebhookScenarioState();
        var queue = new MockWebhookDeliveryQueue();
        var refundId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var request = new RefundPaymentRequest(
            paymentId,
            Guid.NewGuid(),
            refundId,
            35m,
            "USD");

        state.ConfigureRefund(
            refundId,
            MockWebhookScenario.DelayedWebhook);

        var dispatcher = CreateDispatcher(
            state,
            queue);

        await dispatcher.ScheduleRefundAsync(
            request,
            new MockRefundDecision(
                MockRefundScenario.Success,
                MockRefundOutcome.Succeeded,
                $"mock-refund-{refundId:N}",
                1,
                false),
            TestContext.Current.CancellationToken);

        Assert.True(queue.TryRead(out var delivery));
        Assert.NotNull(delivery);
        Assert.Equal(
            "Refund",
            delivery.Payload.OperationType);
        Assert.Equal(
            refundId,
            delivery.Payload.RefundId);
        Assert.Equal(
            TimeSpan.FromSeconds(2),
            delivery.Delay);
        Assert.False(
            queue.TryRead(out _));
    }

    [Fact]
    public async Task IdempotentReplayDoesNotScheduleAnotherWebhook()
    {
        var state = new MockWebhookScenarioState();
        var queue = new MockWebhookDeliveryQueue();
        var paymentId = Guid.NewGuid();

        state.ConfigureCapture(
            paymentId,
            MockWebhookScenario.DuplicateWebhook);

        var dispatcher = CreateDispatcher(
            state,
            queue);

        await dispatcher.ScheduleCaptureAsync(
            new CapturePaymentRequest(
                paymentId,
                Guid.NewGuid(),
                35m,
                "USD"),
            new MockCaptureDecision(
                MockPaymentScenario.Success,
                MockCaptureOutcome.Succeeded,
                $"mock-capture-{paymentId:N}",
                2,
                true),
            TestContext.Current.CancellationToken);

        Assert.False(
            queue.TryRead(out _));
    }

    [Theory]
    [InlineData(
        "delayed_webhook",
        MockWebhookScenario.DelayedWebhook)]
    [InlineData(
        "duplicate_webhook",
        MockWebhookScenario.DuplicateWebhook)]
    public void ParserAcceptsDocumentedWebhookScenarioNames(
        string value,
        MockWebhookScenario expected)
    {
        Assert.True(
            MockWebhookScenarioParser.TryParse(
                value,
                out var actual));
        Assert.Equal(
            expected,
            actual);
    }

    private static MockWebhookDispatcher CreateDispatcher(
        MockWebhookScenarioState state,
        MockWebhookDeliveryQueue queue)
    {
        return new MockWebhookDispatcher(
            state,
            queue,
            new MockPaymentProviderOptions(
                TimeSpan.FromSeconds(10),
                new Uri(
                    "http://localhost:8086/provider/webhooks"),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(50)),
            TimeProvider.System);
    }
}
