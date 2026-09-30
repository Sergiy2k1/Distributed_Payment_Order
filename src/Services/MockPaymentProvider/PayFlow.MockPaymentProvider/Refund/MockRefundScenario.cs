namespace PayFlow.MockPaymentProvider.Refund;

public enum MockRefundScenario
{
    Success = 0,
    Reject = 1,
    TimeoutBeforeProcessing = 2,
    TimeoutAfterProcessing = 3,
    ServerErrorThenSuccess = 4
}
