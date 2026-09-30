namespace PayFlow.Saga.Application.Checkout;

public enum CheckoutSagaTimeoutAction
{
    PostCaptureCompensationStarted = 0,
    RequiresReconciliation = 1,
    RecoveryAlreadyInProgress = 2,
    NoAction = 3,
    ReconciliationRequested = 4
}
