namespace PayFlow.MockPaymentProvider.Capture;

public static class MockPaymentScenarioParser
{
    public static bool TryParse(
        string? value,
        out MockPaymentScenario scenario)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "success":
                scenario = MockPaymentScenario.Success;
                return true;

            case "decline":
                scenario = MockPaymentScenario.Decline;
                return true;

            case "timeout_before_processing":
                scenario = MockPaymentScenario.TimeoutBeforeProcessing;
                return true;

            case "timeout_after_processing":
                scenario = MockPaymentScenario.TimeoutAfterProcessing;
                return true;

            case "500_then_success":
                scenario = MockPaymentScenario.ServerErrorThenSuccess;
                return true;

            default:
                scenario = default;
                return false;
        }
    }
}
