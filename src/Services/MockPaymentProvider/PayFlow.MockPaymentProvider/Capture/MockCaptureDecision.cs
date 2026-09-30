namespace PayFlow.MockPaymentProvider.Capture;

public sealed record MockCaptureDecision(
    MockPaymentScenario Scenario,
    string? ProviderReference,
    bool IsIdempotentReplay);
