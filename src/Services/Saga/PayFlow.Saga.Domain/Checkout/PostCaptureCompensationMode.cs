namespace PayFlow.Saga.Domain.Checkout;

public enum PostCaptureCompensationMode
{
    ReleaseReservedInventory = 0,
    RestockConsumedInventory = 1
}
