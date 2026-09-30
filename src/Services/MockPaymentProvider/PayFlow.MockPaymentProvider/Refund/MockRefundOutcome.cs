namespace PayFlow.MockPaymentProvider.Refund;

public enum MockRefundOutcome
{
    Succeeded = 0,
    Rejected = 1,
    TimeoutBeforeProcessing = 2,
    TimeoutAfterProcessing = 3,
    ServerError = 4
}
