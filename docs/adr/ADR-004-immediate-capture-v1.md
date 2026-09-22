# ADR-004: Immediate Capture in V1

## Status

Accepted.

## Context

The payment section of the PayFlow specification includes concepts associated with both authorization and capture.

A production payment system may separate:

1. authorization — reserve/approve funds;
2. capture — actually settle the charge.

That model is useful when the merchant needs to verify inventory, fraud checks, shipping readiness, or another condition between authorization and capture.

However, the initial PayFlow checkout already includes substantial distributed-system complexity:

- Kafka delivery and ordering;
- Transactional Outbox;
- Inbox deduplication;
- Saga orchestration;
- inventory reservation and consumption;
- provider retries;
- ambiguous payment outcomes;
- reconciliation;
- refunds;
- immutable double-entry ledger;
- crash/restart recovery.

Introducing authorization and capture as two independent provider operations in the first executable version would add another set of states, provider idempotency keys, timeout cases, expirations, compensations, and reconciliation paths before the baseline reliability model is proven.

## Decision

PayFlow V1 uses **immediate capture** semantics.

The initial logical payment flow is:

```text
Created
  -> Processing
  -> Captured

Processing
  -> Failed
```

Refund is modeled separately after a successful capture:

```text
Captured
  -> RefundPending
  -> Refunded
```

The command used by Saga is:

```text
CapturePayment.v1
```

The Payment service performs one logical provider capture operation using a deterministic provider idempotency key.

V1 does not expose an authorization hold as a required checkout step.

## Meaning of "immediate capture"

"Immediate capture" does not mean the Payment service performs an unsafe one-shot HTTP call.

The operation still requires:

- durable local Payment state;
- deterministic provider idempotency;
- timeout handling;
- reconciliation of ambiguous outcomes;
- immutable ledger posting;
- retry/recovery metadata;
- duplicate webhook handling;
- Outbox publication after local state is committed.

The simplification is only that V1 has one charge-producing provider operation instead of a required authorization operation followed by a separate capture operation.

## V1 checkout sequence

The relevant happy-path sequence is:

```text
InventoryReserved.v1
    |
    v
CapturePayment.v1
    |
    v
PaymentCaptured.v1
    |
    v
ConsumeInventory.v1
    |
    v
InventoryConsumed.v1
    |
    v
ConfirmOrder.v1
```

This intentionally captures payment before inventory reservation is consumed.

The reservation protects the stock while Payment is being processed.

If payment definitively fails, the reservation is released.

If payment succeeds but the workflow later becomes permanently unrecoverable, explicit compensation is required.

## Provider operation identity

The logical capture operation must reuse one deterministic provider key across all retries.

Conceptually:

```text
payment:{PaymentId}:capture:v1
```

The exact serialization format may change, but the identity must remain stable for the same logical capture.

A retry must not create a new logical provider operation.

## Ambiguous capture outcome

The following case is expected:

1. Payment sends the capture request.
2. The provider captures successfully.
3. The response is lost.
4. Payment receives a timeout.

The state remains:

```text
Processing
```

until an authoritative result is obtained through:

- retry with the same provider idempotency key; or
- reconciliation/query against the provider.

The system must not interpret timeout as a definitive decline.

## Ledger implication

A successful logical capture creates the corresponding financial ledger transaction exactly once at the business level.

Duplicate:

- commands;
- provider responses;
- webhooks;
- reconciliation findings

must not create an additional ledger posting.

The ledger remains append-only.

## Compensation implication

Because V1 captures funds rather than merely authorizing them, a permanent failure after capture requires an explicit refund.

Examples:

### Payment captured, inventory reservation still held, checkout cannot continue

```text
RefundPayment.v1
  -> PaymentRefunded.v1
  -> ReleaseInventory.v1
  -> InventoryReleased.v1
  -> CancelOrder.v1
```

### Payment captured, inventory already consumed, Order cannot be confirmed permanently

```text
RefundPayment.v1
  -> PaymentRefunded.v1
  -> RestockInventory.v1
  -> InventoryRestocked.v1
  -> CancelOrder.v1
```

A captured payment is never "forgotten" by moving Payment directly to a failed state.

## Alternatives considered

### Separate authorization and capture from day one

Example lifecycle:

```text
Created
  -> Authorizing
  -> Authorized
  -> Capturing
  -> Captured
```

Potential benefits:

- funds can be held before final commitment;
- capture can happen only after inventory/business validation;
- some compensation may use authorization void instead of refund;
- the model is closer to many production card-payment flows.

Deferred for V1 because it would require additional design and implementation for:

- authorization idempotency identity;
- capture idempotency identity;
- authorization expiration;
- provider void operation;
- partial authorization;
- authorization timeout reconciliation;
- capture timeout reconciliation;
- capture-after-expired-authorization behaviour;
- Saga states for authorization vs capture;
- ledger treatment before and after capture;
- additional mock-provider scenarios.

Those are valuable topics, but adding them before the baseline workflow works would increase breadth faster than depth.

### Authorization-only V1

Rejected because the project is intended to demonstrate actual financial completion, refunds, ledger posting, and reconciliation around a completed charge.

Stopping at authorization would leave the most important settlement-related paths unimplemented.

### Capture only after Order confirmation

Rejected because Order confirmation is intended to represent that the distributed checkout succeeded.

If the Order became confirmed before the payment was definitively captured, Order could claim success while Payment later failed.

The authoritative Payment result must be known before final Order confirmation.

### Capture before reserving inventory

Rejected because successful payment could occur before the system knows whether stock can be held.

The baseline instead reserves inventory first and captures only while a valid reservation exists.

## Consequences

Positive consequences:

- fewer provider operations in the first executable workflow;
- smaller Payment and Saga state machines;
- fewer ambiguous timeout combinations;
- reconciliation can focus on capture and refund;
- the ledger model is easier to validate;
- the project can go deeper on reliability instead of adding premature breadth;
- a future authorization/capture split remains a meaningful extension.

Negative consequences:

- some permanent post-capture failures require refund rather than authorization void;
- the provider may hold/settle funds earlier than a two-stage model would;
- the model does not initially demonstrate authorization expiry or void;
- adding authorization later will require contract and state-machine evolution.

## Future authorization/capture split

A future version may introduce separate operations such as:

```text
AuthorizePayment.v2
PaymentAuthorized.v2
CaptureAuthorizedPayment.v2
PaymentCaptured.v2
VoidAuthorization.v2
PaymentAuthorizationVoided.v2
```

The exact names are not committed by this ADR.

Before introducing that version, the design must explicitly define:

- authorization state machine;
- authorization expiration;
- void semantics;
- provider identities for authorize/capture/void;
- how retries map to those identities;
- ledger treatment for authorization vs captured money;
- Saga changes;
- backwards compatibility with V1 contracts;
- reconciliation for every ambiguous external operation.

## Migration principle

V1 state names and contracts must not be silently reinterpreted.

For example, `PaymentCaptured.v1` must continue to mean that funds were actually captured according to V1 semantics.

If authorization is added, new contracts/states must preserve that meaning rather than changing what existing V1 messages represent.

## Testing implications

V1 tests must cover at minimum:

- successful capture;
- definitive provider decline;
- timeout before provider processing;
- timeout after provider processing;
- transient provider error followed by success;
- duplicate capture command;
- duplicate provider webhook;
- delayed webhook;
- reconciliation finding a successful capture after timeout;
- refund after successful capture;
- duplicate refund command;
- duplicate refund result;
- one logical capture creates one ledger posting;
- one logical refund creates one reversing ledger transaction.

## Review trigger

Revisit this ADR when the baseline immediate-capture workflow is stable and one of these requirements becomes concrete:

- delayed settlement;
- shipping or fulfillment occurs significantly after checkout;
- fraud/manual review must happen between authorization and capture;
- provider authorization expiration needs to be demonstrated;
- authorization void is materially preferable to refund;
- interview/portfolio scope specifically requires two-stage payment semantics after core reliability is already proven.

The extension should be added because the domain requires it, not merely to increase the number of payment states.
