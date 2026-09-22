# ADR-010: Payment Provider Idempotency and Reconciliation

## Status

Accepted for V1.

## Context

Payment provider calls can fail in ways where PayFlow cannot immediately know whether the provider processed the operation.

Example:

1. PayFlow sends a capture request.
2. The provider captures the payment.
3. The response is lost.
4. PayFlow sees a timeout.

If PayFlow retries with a new provider operation identity, the customer may be charged twice.

If PayFlow assumes timeout means failure, local state may diverge from the provider.

The same class of ambiguity exists for refunds.

## Decision

Every logical provider operation uses a **stable deterministic idempotency key** that is reused across retries, restarts, and reconciliation.

Conceptually:

```text
payment:{PaymentId}:capture:v1
refund:{RefundId}:refund:v1
```

The exact string format may evolve, but the identity must remain stable for the same logical operation.

A timeout or transport error does **not** automatically transition the business operation to Failed.

Ambiguous operations remain unresolved until PayFlow obtains an authoritative provider result.

## ProviderOperation record

Payment persists a provider-operation record for every external financial action.

Expected fields include:

- `ProviderOperationId`;
- `OperationType`;
- `BusinessOperationId`;
- `ProviderIdempotencyKey`;
- `ProviderReference` when known;
- `Status`;
- `AttemptCount`;
- `LastAttemptAtUtc`;
- `NextAttemptAtUtc`;
- `LastErrorCode`;
- `LastKnownProviderStatus`;
- timestamps.

The provider operation belongs to Payment and is stored in `payments_db`.

## Capture behaviour

For `CapturePayment.v1`:

1. Payment creates or reuses the logical Payment and ProviderOperation.
2. The same provider idempotency key is used on every attempt.
3. Provider success transitions Payment to `Captured`.
4. Definitive provider decline transitions Payment to `Failed`.
5. Timeout/connection loss keeps Payment unresolved, normally `Processing`.
6. Reconciliation later determines the authoritative outcome if retry cannot.

## Refund behaviour

For `RefundPayment.v1`:

1. Refund has its own stable `RefundId`.
2. Refund gets its own deterministic provider idempotency key.
3. Provider success transitions the Refund operation to success and Payment to `Refunded` where applicable.
4. Definitive rejection is recorded as a provider/business outcome.
5. Timeout remains ambiguous and is reconciled.

A retry must never create a new logical refund.

## Outcome classification

### Definitive success

Examples:

- provider returns a successful capture;
- provider query confirms captured;
- provider webhook confirms capture and matches the same logical operation.

Action:

- apply the authoritative success transition idempotently;
- post the ledger effect exactly once;
- publish the corresponding integration event through Outbox.

### Definitive business/provider failure

Examples:

- card/payment decline;
- provider explicitly says the operation was not accepted and was not processed.

Action:

- transition to the appropriate failed/rejected state;
- publish the business failure event.

### Ambiguous technical outcome

Examples:

- request timeout;
- connection reset after request transmission;
- provider 500 where processing status is unknown;
- delayed or missing webhook.

Action:

- keep the operation unresolved;
- retry with the same provider key when safe;
- schedule reconciliation;
- do not emit a false failure event.

## Reconciliation worker

Payment includes a reconciliation process for ambiguous or stale provider operations.

It scans for operations such as:

- Payment = `Processing` beyond expected response time;
- Refund = `Processing` / `ReconciliationRequired`;
- provider attempt with timeout-after-processing risk;
- operation with missing final webhook.

For each candidate, reconciliation:

1. loads the persisted provider operation;
2. queries the provider using stable provider identity/reference;
3. classifies the authoritative result;
4. applies the same domain transition that normal request handling would apply;
5. uses the same logical ledger posting identity;
6. writes integration events through Outbox;
7. records reconciliation metadata.

Reconciliation is not a parallel alternative business flow.

It is another way to discover the authoritative outcome of the same logical operation.

## Idempotency interaction

Provider idempotency and local idempotency are both required.

Provider idempotency prevents the external system from applying the same logical operation twice.

Local idempotency prevents PayFlow from applying the returned result twice.

Both are necessary because:

- the provider may deduplicate correctly while PayFlow receives duplicate responses/webhooks;
- PayFlow may deduplicate messages while a badly retried provider call creates a second external operation.

## Webhook handling

Provider webhooks are treated as at-least-once external messages.

The Payment service must:

- authenticate/verify webhook origin according to provider capabilities;
- identify the logical provider operation;
- deduplicate webhook delivery;
- reject or quarantine contradictory/unmatchable data;
- apply only legal state transitions;
- never create a second ledger posting for the same logical operation.

Webhook arrival order is not assumed.

A delayed webhook after reconciliation must become an idempotent no-op if the same authoritative outcome was already applied.

## Stable operation identity

A logical provider operation must not derive its idempotency key from:

- retry attempt number;
- current timestamp;
- random GUID generated per HTTP call;
- process instance;
- Kafka MessageId.

Those identify attempts/messages, not the logical business operation.

The provider key is derived from the stable Payment/Refund business identity.

## Retry policy

Retries are bounded and classify failures.

Immediate/bounded retry may be used for clearly transient transport problems.

Retry must preserve:

- same provider idempotency key;
- same logical amount/currency;
- same operation type.

If retries cannot resolve the outcome, reconciliation takes over.

The exact retry count and delays are configuration, not business semantics.

## Manual intervention

If the provider cannot return an authoritative result after automated reconciliation, the operation may transition to a manual-review state.

Manual intervention must preserve:

- original provider key;
- original provider reference;
- operation history;
- all attempts;
- reconciliation findings.

An operator must not "fix" ambiguity by issuing a new capture/refund with a new logical identity unless an explicit business procedure authorizes a separate operation.

## Ledger interaction

Provider success and ledger posting are coupled by the local Payment transaction.

For capture:

```text
provider outcome = success
       |
       v
BEGIN local DB transaction
  Payment -> Captured
  Ledger capture posting
  Outbox(PaymentCaptured.v1)
COMMIT
```

For refund:

```text
provider outcome = success
       |
       v
BEGIN local DB transaction
  Refund -> Succeeded
  Payment -> Refunded
  Ledger reversal posting
  Outbox(PaymentRefunded.v1)
COMMIT
```

If the local transaction fails after the provider succeeded, reconciliation must later re-apply the same logical success locally without creating another provider operation.

## Crash scenario

Expected failure:

1. provider capture succeeds;
2. process crashes before local Payment transaction commits;
3. service restarts;
4. local state still says Processing;
5. reconciliation queries provider;
6. provider confirms capture;
7. local Payment + Ledger + Outbox commit once.

This scenario must not create a second external charge.

## Alternatives considered

### Treat timeout as failure

Rejected because timeout means PayFlow does not know the outcome.

It can produce a false failure while the provider actually captured funds.

### Retry with a new idempotency key

Rejected because it can create duplicate financial operations.

### Rely only on provider webhooks

Rejected because webhooks may be delayed, duplicated, misconfigured, or temporarily unavailable.

Polling/query reconciliation remains required for unresolved operations.

### Rely only on synchronous provider response

Rejected because responses can be lost after successful provider processing.

### Store provider state only in logs

Rejected because logs are not durable business recovery state.

Provider operations must be persisted in `payments_db`.

## Mock Provider requirements

The Mock Payment Provider must support deterministic scenarios for:

- success;
- decline;
- timeout before processing;
- timeout after processing;
- transient 500 then success;
- duplicate webhook;
- delayed webhook.

It must also honor the same idempotency key semantics so retry safety can be tested end to end.

## Observability requirements

Payment telemetry must expose:

- provider request count;
- provider attempt count;
- timeout count;
- ambiguous-operation count;
- reconciliation scan count;
- reconciliation success/failure count;
- duplicate webhook count;
- provider idempotency conflicts;
- operation age while unresolved.

Logs/traces should include:

- `PaymentId`;
- `RefundId` when applicable;
- `OrderId`;
- provider operation identity;
- provider reference;
- `CorrelationId`.

Sensitive payment data must not be logged.

## Required tests

At minimum, tests must prove:

- retry after timeout-before-processing creates one logical provider operation;
- retry after timeout-after-processing does not double charge;
- process crash after provider success but before local commit recovers through reconciliation;
- duplicate webhook is a no-op;
- delayed webhook after reconciliation is a no-op;
- provider 500 then success reuses the same idempotency key;
- capture reconciliation produces one ledger posting;
- refund reconciliation produces one reversal posting;
- a definitive decline produces `PaymentFailed.v1`;
- a timeout does not produce `PaymentFailed.v1`;
- manual-review state preserves provider identity/history.

## Consequences

Positive consequences:

- double charge/refund risk is reduced;
- ambiguous provider outcomes are modeled truthfully;
- crash recovery remains possible;
- provider retries are deterministic;
- reconciliation has a clear role;
- webhooks and synchronous responses converge on one domain transition path.

Negative consequences:

- Payment persistence is more complex;
- reconciliation workers require scheduling and observability;
- unresolved operations may remain pending longer;
- provider-specific APIs must support lookup/idempotency semantics;
- manual-review tooling may eventually be required.

These costs are justified because ambiguity is unavoidable in real external financial integrations.

## Implementation constraints

When Payment provider integration begins:

1. every external financial operation has one stable provider idempotency key;
2. retries reuse that key;
3. timeout is not equivalent to failure;
4. ProviderOperation state is persisted;
5. reconciliation can query unresolved operations;
6. provider success is applied through one idempotent local transition path;
7. duplicate webhook/response/reconciliation result cannot duplicate ledger effects;
8. Payment/Refund state + Ledger + Outbox commit atomically;
9. process restart must not lose unresolved operation identity;
10. manual review preserves the original operation history.

## Review trigger

Revisit this ADR if a real provider has materially different idempotency or lookup capabilities.

Any provider-specific adaptation must still preserve the core rule:

**one logical business operation must not become multiple external financial operations because of retry or uncertainty.**
