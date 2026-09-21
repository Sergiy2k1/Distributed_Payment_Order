# State Machines

## Status

Accepted as the initial PayFlow state-transition baseline.

The goal of these state machines is to make legal transitions explicit before implementation. They describe business state, recovery behaviour, and terminal conditions. Technical retries must not invent new business transitions.

## Global transition rules

All stateful components follow these rules:

- state changes are persisted before externally visible follow-up work is considered complete;
- duplicate commands or events must be safe to process more than once;
- a duplicate that represents an already-applied transition is a no-op, not a second transition;
- an illegal transition is rejected and recorded rather than silently accepted;
- technical failures normally keep the current business state and update retry/recovery metadata;
- optimistic concurrency or equivalent database protection must prevent two workers from applying conflicting transitions;
- terminal states are immutable unless an explicit later workflow creates a new aggregate or operation;
- process restart must not lose the current state or deadline.

## Order state machine

The Order bounded context should expose business state without mirroring every internal Saga step.

### States

- `Pending` — order has been created but checkout processing has not started.
- `Processing` — distributed checkout is in progress.
- `Confirmed` — checkout succeeded and the order is accepted.
- `Cancelling` — compensation is being finalized before cancellation becomes authoritative.
- `Cancelled` — checkout did not complete and the order is cancelled.
- `RefundRequested` — a confirmed order has entered the refund workflow.
- `Refunded` — the baseline full-refund workflow completed.
- `Fulfilled` — the order has been fulfilled.

### Allowed transitions

```text
Pending
  -> Processing
  -> Cancelling

Processing
  -> Confirmed
  -> Cancelling

Cancelling
  -> Cancelled

Confirmed
  -> RefundRequested
  -> Fulfilled

RefundRequested
  -> Refunded
```

### Terminal states

For the baseline workflow:

- `Cancelled`
- `Refunded`
- `Fulfilled`

`Confirmed` is stable but not terminal because refund or fulfillment may follow.

### Important rules

- Order does not expose `AwaitingInventory` or `AwaitingPayment`; those are orchestration details owned by Saga.
- An order must not become `Cancelled` until required compensation has reached a safe point.
- Once `Confirmed`, a payment reversal is represented through the refund flow, not by pretending the original checkout never happened.
- Replaying `ConfirmOrder` for an already confirmed order must be idempotent.

## Inventory reservation state machine

A reservation protects stock while checkout is in progress.

### States

- `Pending` — reservation record exists but the stock decision is not finalized.
- `Reserved` — stock is successfully held for the order.
- `Rejected` — inventory could not be reserved.
- `Consumed` — the reservation has been converted into committed stock consumption.
- `Released` — reserved stock was returned during compensation.
- `Expired` — the reservation deadline elapsed before successful completion.

### Allowed transitions

```text
Pending
  -> Reserved
  -> Rejected

Reserved
  -> Consumed
  -> Released
  -> Expired
```

### Terminal states

- `Rejected`
- `Consumed`
- `Released`
- `Expired`

### Important rules

- `Reserved` is not terminal; held stock must eventually be consumed, released, or expire.
- `Consumed` prevents successful checkout from leaving stock permanently reserved.
- a release command against an already released reservation is a no-op;
- a reserve retry for the same logical reservation must never reserve stock twice;
- `Reserved -> Consumed` and `Reserved -> Released` are mutually exclusive and require database concurrency protection;
- expiration is a business transition driven by a persisted deadline, not an in-memory timer.

The exact command/event names for consuming inventory will be fixed in the integration contract catalog.

## Payment state machine

### V1 semantic decision

The initial PayFlow checkout uses **immediate capture** semantics.

That means V1 does not model a separate authorization hold as a required checkout step. The baseline payment lifecycle is:

```text
Created -> Processing -> Captured
                      -> Failed
```

A future authorization/capture split may add states such as `Authorizing`, `Authorized`, and `Capturing`. That extension must be introduced through an explicit architecture decision rather than by overloading V1 states.

### States

- `Created` — local payment exists but no provider operation has started.
- `Processing` — a capture operation has been initiated or its result is being reconciled.
- `Captured` — the provider has definitively captured the payment.
- `Failed` — the provider definitively rejected or failed the logical payment without capturing it.
- `RefundPending` — a refund workflow is active.
- `Refunded` — the baseline full refund succeeded.

### Allowed transitions

```text
Created
  -> Processing

Processing
  -> Captured
  -> Failed

Captured
  -> RefundPending

RefundPending
  -> Refunded
```

### Important rules

- timeout does not automatically mean `Failed`;
- timeout after provider processing is ambiguous and remains `Processing` until retry or reconciliation proves the outcome;
- retries reuse the same deterministic provider operation idempotency key;
- duplicate webhook or event delivery must not create a second capture;
- `Captured` can only be reached with provider evidence that the logical operation succeeded;
- ledger posting must be idempotent with the payment transition;
- a technical retry must not transition `Processing -> Failed` merely because retry budget was temporarily exhausted.

## Refund operation state machine

Refund is modeled separately because provider refund processing has its own retries and ambiguous outcomes.

### States

- `Requested` — refund intent has been accepted.
- `Processing` — provider refund operation is active or unresolved.
- `Succeeded` — provider confirms the refund.
- `Rejected` — provider definitively rejected the refund as a business/provider outcome.
- `ReconciliationRequired` — the provider outcome is ambiguous and must be queried.
- `ManualReview` — automation cannot safely determine or repair the result.

### Allowed transitions

```text
Requested
  -> Processing

Processing
  -> Succeeded
  -> Rejected
  -> ReconciliationRequired

ReconciliationRequired
  -> Processing
  -> Succeeded
  -> Rejected
  -> ManualReview
```

### Important rules

- a timeout after sending the refund does not permit a second logical refund with a new provider key;
- reconciliation uses the original provider operation identity;
- the Payment aggregate moves to `Refunded` only after the refund operation reaches `Succeeded`;
- ledger reversal entries are appended only once for a successful logical refund.

V1 assumes a full refund. Partial and multiple refunds are deferred until their accounting invariants are designed explicitly.

## Checkout Saga state machine

Saga owns orchestration progress. It does not own Order, Inventory, or Payment business truth.

### States

- `Started`
- `WaitingForInventory`
- `WaitingForPayment`
- `WaitingForInventoryCommit`
- `WaitingForOrderConfirmation`
- `CompensatingPayment`
- `CompensatingInventory`
- `WaitingForOrderCancellation`
- `Completed`
- `CompletedWithBusinessFailure`
- `ManualInterventionRequired`

### Happy path

```text
Started
  -> WaitingForInventory
  -> WaitingForPayment
  -> WaitingForInventoryCommit
  -> WaitingForOrderConfirmation
  -> Completed
```

The happy path means:

1. reserve inventory;
2. capture payment;
3. consume the successful inventory reservation;
4. confirm the order.

This additional inventory-consumption step prevents a successful order from leaving stock in a permanently reserved state.

### Business-failure compensation

Inventory rejection:

```text
WaitingForInventory
  -> WaitingForOrderCancellation
  -> CompletedWithBusinessFailure
```

Payment definitive failure after inventory reservation:

```text
WaitingForPayment
  -> CompensatingInventory
  -> WaitingForOrderCancellation
  -> CompletedWithBusinessFailure
```

Inventory can no longer be safely committed after payment was captured:

```text
WaitingForInventoryCommit
  -> CompensatingPayment
  -> CompensatingInventory
  -> WaitingForOrderCancellation
  -> CompletedWithBusinessFailure
```

### Technical failures

Technical failures such as Kafka unavailability, process crash, transient database failure, or provider timeout normally do **not** create a new business Saga state.

Instead, Saga persists:

- current step;
- retry count;
- next-attempt time;
- deadline;
- last technical error metadata.

The workflow remains in the same waiting/compensation state and resumes after recovery.

If automation can no longer determine a safe action before the configured operational deadline, Saga transitions to `ManualInterventionRequired`.

### Important rules

- each received integration message is deduplicated;
- the Saga version is concurrency protected;
- duplicate successful events are no-ops;
- an event that contradicts the persisted state is not silently applied;
- a process restart resumes from persisted state;
- compensation is idempotent;
- payment is never captured again merely because Order confirmation is temporarily unavailable;
- once payment is captured, a permanent inability to finish checkout requires an explicit refund path rather than forgetting the charge.

## Transition ownership

Only the owner may authoritatively change its state:

| State machine | Owner |
| --- | --- |
| Order | Order service |
| Inventory Reservation | Inventory service |
| Payment | Payment service |
| Refund operation | Payment service |
| Checkout Saga | Saga service |

Saga may request transitions by command, but the owning service decides and persists the actual transition.

## State machine review checklist

Before implementing a handler for a state transition, verify:

1. Is the current state valid for this transition?
2. Is the command/event idempotent?
3. Can the same message arrive twice?
4. What happens if the process crashes after the database commit but before publication?
5. What database constraint or concurrency mechanism prevents conflicting transitions?
6. Is the target state terminal?
7. If the external outcome is ambiguous, are we preserving ambiguity instead of guessing failure?
8. Does compensation reverse business effects with a new explicit operation rather than deleting history?
