using System.Net.Http.Json;
using PayFlow.Payment.Application.Provider;
using PayFlow.Payment.Infrastructure.Provider;

namespace PayFlow.Payment.IntegrationTests.Provider;

public sealed class HttpPaymentProviderBoundaryTests(
    MockPaymentProviderWebApplicationFactory factory)
    : IClassFixture<MockPaymentProviderWebApplicationFactory>
{
    [Fact]
    public async Task CaptureTimeoutAfterProcessingIsRecoveredByIdempotentReplay()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId =
            Guid.NewGuid();
        var request =
            new PaymentProviderCaptureRequest(
                paymentId,
                Guid.NewGuid(),
                35m,
                "USD",
                $"capture-{paymentId:N}");

        await ConfigureCaptureScenarioAsync(
            factory,
            paymentId,
            "timeout_after_processing",
            cancellationToken);

        using var shortClient =
            factory.CreateClient();
        shortClient.Timeout =
            TimeSpan.FromMilliseconds(50);

        var first =
            await new HttpPaymentProvider(shortClient)
                .CaptureAsync(
                    request,
                    cancellationToken);

        Assert.Equal(
            PaymentProviderCaptureOutcome.Ambiguous,
            first.Outcome);
        Assert.Equal(
            "TIMEOUT",
            first.ErrorCode);

        using var replayClient =
            factory.CreateClient();
        replayClient.Timeout =
            TimeSpan.FromSeconds(2);

        var replay =
            await new HttpPaymentProvider(replayClient)
                .CaptureAsync(
                    request,
                    cancellationToken);

        Assert.Equal(
            PaymentProviderCaptureOutcome.Succeeded,
            replay.Outcome);
        Assert.Equal(
            $"mock-capture-{paymentId:N}",
            replay.ProviderReference);
    }

    [Fact]
    public async Task CaptureServerErrorThenSuccessRetriesSameLogicalOperation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var paymentId =
            Guid.NewGuid();
        var request =
            new PaymentProviderCaptureRequest(
                paymentId,
                Guid.NewGuid(),
                35m,
                "USD",
                $"capture-{paymentId:N}");

        await ConfigureCaptureScenarioAsync(
            factory,
            paymentId,
            "500_then_success",
            cancellationToken);

        using var client =
            factory.CreateClient();

        var provider =
            new HttpPaymentProvider(client);

        var first =
            await provider.CaptureAsync(
                request,
                cancellationToken);

        var second =
            await provider.CaptureAsync(
                request,
                cancellationToken);

        Assert.Equal(
            PaymentProviderCaptureOutcome.Ambiguous,
            first.Outcome);
        Assert.Equal(
            "HTTP_500",
            first.ErrorCode);

        Assert.Equal(
            PaymentProviderCaptureOutcome.Succeeded,
            second.Outcome);
        Assert.Equal(
            $"mock-capture-{paymentId:N}",
            second.ProviderReference);
    }

    [Fact]
    public async Task RefundTimeoutAfterProcessingIsRecoveredByIdempotentReplay()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var refundId =
            Guid.NewGuid();
        var request =
            new PaymentProviderRefundRequest(
                refundId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                35m,
                "USD",
                $"refund-{refundId:N}");

        await ConfigureRefundScenarioAsync(
            factory,
            refundId,
            "timeout_after_processing",
            cancellationToken);

        using var shortClient =
            factory.CreateClient();
        shortClient.Timeout =
            TimeSpan.FromMilliseconds(50);

        var first =
            await new HttpPaymentProvider(shortClient)
                .RefundAsync(
                    request,
                    cancellationToken);

        Assert.Equal(
            PaymentProviderRefundOutcome.Ambiguous,
            first.Outcome);
        Assert.Equal(
            "TIMEOUT",
            first.ErrorCode);

        using var replayClient =
            factory.CreateClient();
        replayClient.Timeout =
            TimeSpan.FromSeconds(2);

        var replay =
            await new HttpPaymentProvider(replayClient)
                .RefundAsync(
                    request,
                    cancellationToken);

        Assert.Equal(
            PaymentProviderRefundOutcome.Succeeded,
            replay.Outcome);
        Assert.Equal(
            $"mock-refund-{refundId:N}",
            replay.ProviderReference);
    }

    [Fact]
    public async Task RefundRejectMapsToDefinitiveProviderRejection()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var refundId =
            Guid.NewGuid();
        var request =
            new PaymentProviderRefundRequest(
                refundId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                35m,
                "USD",
                $"refund-{refundId:N}");

        await ConfigureRefundScenarioAsync(
            factory,
            refundId,
            "reject",
            cancellationToken);

        using var client =
            factory.CreateClient();

        var actual =
            await new HttpPaymentProvider(client)
                .RefundAsync(
                    request,
                    cancellationToken);

        Assert.Equal(
            PaymentProviderRefundOutcome.DefinitivelyRejected,
            actual.Outcome);
        Assert.Equal(
            "HTTP_422",
            actual.ErrorCode);
    }

    private static async Task ConfigureCaptureScenarioAsync(
        MockPaymentProviderWebApplicationFactory factory,
        Guid paymentId,
        string scenario,
        CancellationToken cancellationToken)
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.PutAsJsonAsync(
                $"/scenarios/payments/{paymentId:D}",
                new
                {
                    Scenario = scenario
                },
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task ConfigureRefundScenarioAsync(
        MockPaymentProviderWebApplicationFactory factory,
        Guid refundId,
        string scenario,
        CancellationToken cancellationToken)
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.PutAsJsonAsync(
                $"/scenarios/refunds/{refundId:D}",
                new
                {
                    Scenario = scenario
                },
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
