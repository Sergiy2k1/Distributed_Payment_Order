namespace PayFlow.MockPaymentProvider.Capture;

public sealed record MockCaptureDecision(
    MockPaymentScenario Scenario,
    MockCaptureOutcome Outcome,
    string? ProviderReference,
    int AttemptNumber,
    bool IsIdempotentReplay);
