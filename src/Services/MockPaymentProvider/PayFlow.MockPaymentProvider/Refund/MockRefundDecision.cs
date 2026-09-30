namespace PayFlow.MockPaymentProvider.Refund;

public sealed record MockRefundDecision(
    MockRefundScenario Scenario,
    MockRefundOutcome Outcome,
    string? ProviderReference,
    int AttemptNumber,
    bool IsIdempotentReplay);
