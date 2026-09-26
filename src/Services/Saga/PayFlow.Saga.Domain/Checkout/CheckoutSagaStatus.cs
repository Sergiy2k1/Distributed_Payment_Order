namespace PayFlow.Saga.Domain.Checkout;

public enum CheckoutSagaStatus
{
    Started = 0,
    WaitingForInventory = 1,
    WaitingForPayment = 2,
    WaitingForInventoryCommit = 3,
    WaitingForOrderConfirmation = 4,
    CompensatingPayment = 5,
    CompensatingInventory = 6,
    CompensatingInventoryRestock = 7,
    WaitingForOrderCancellation = 8,
    Completed = 9,
    CompletedWithBusinessFailure = 10,
    ManualInterventionRequired = 11
}
