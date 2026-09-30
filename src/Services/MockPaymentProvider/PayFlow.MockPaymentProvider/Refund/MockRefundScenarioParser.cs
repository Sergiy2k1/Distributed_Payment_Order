namespace PayFlow.MockPaymentProvider.Refund;

public static class MockRefundScenarioParser
{
    public static bool TryParse(
        string? value,
        out MockRefundScenario scenario)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "success":
                scenario = MockRefundScenario.Success;
                return true;

            case "reject":
                scenario = MockRefundScenario.Reject;
                return true;

            case "timeout_before_processing":
                scenario = MockRefundScenario.TimeoutBeforeProcessing;
                return true;

            case "timeout_after_processing":
                scenario = MockRefundScenario.TimeoutAfterProcessing;
                return true;

            case "500_then_success":
                scenario = MockRefundScenario.ServerErrorThenSuccess;
                return true;

            default:
                scenario = default;
                return false;
        }
    }
}
