namespace PayFlow.MockPaymentProvider.Capture;

public enum MockCaptureOutcome
{
    Succeeded = 0,
    Declined = 1,
    TimeoutBeforeProcessing = 2,
    TimeoutAfterProcessing = 3,
    ServerError = 4
}
