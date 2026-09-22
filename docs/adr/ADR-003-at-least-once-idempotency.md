# ADR-003: At-Least-Once Delivery with Idempotent Processing

## Status

Accepted.

## Context

PayFlow uses Kafka for asynchronous integration between independently owned services.

The system must tolerate failures such as:

- producer crash after Kafka accepts a record but before local Outbox state is marked published;
- consumer crash after its local database transaction commits but before the Kafka offset commits;
- consumer-group rebalance during processing;
- temporary network interruption;
- manual DLQ replay;
- webhook redelivery from the payment provider;
- retry after an ambiguous provider timeout.

These failures make duplicate delivery a normal operating condition.

Attempting to treat transport-level delivery as exactly-once business execution would create false confidence, especially because PayFlow also interacts with PostgreSQL and an external payment provider outside one Kafka transaction boundary.

## Decision

PayFlow assumes **at-least-once delivery** and implements **idempotent business processing**.

The target is effectively-once business behaviour, not a claim of end-to-end exactly-once transport.

The baseline combines:

- Transactional Outbox for reliable publication;
- Inbox / processed-message deduplication for consumers;
- business idempotency keys for logical operations;
- unique database constraints;
- legal state-transition checks;
- optimistic concurrency or equivalent protection;
- deterministic external-provider operation keys;
- safe replay of duplicate messages.

Duplicate delivery is expected and must not change the final business result.

## Different identities solve different problems

PayFlow does not use one identifier for every form of deduplication.

### Message identity

`MessageId` identifies one integration message.

It is used by the Inbox to detect redelivery of the same published message.

Example:

```text
(MessageId, ConsumerName) -> already processed?
```

### Business operation identity

A logical business operation has its own stable identity.

Examples:

- create Order -> HTTP `Idempotency-Key`;
- reserve Inventory -> `ReservationId`;
- capture Payment -> `PaymentId` / deterministic provider operation key;
- refund Payment -> `RefundId`;
- restock Inventory -> `RestockOperationId`.

A new Kafka `MessageId` must not allow the same logical business operation to execute twice.

## Producer boundary

Business state and the Outbox record are committed in one local database transaction.

```text
business mutation
      +
OutboxMessage
      |
      | same DB transaction
      v
COMMIT
```

A background Outbox publisher later sends the record to Kafka.

If Kafka accepts the message but the publisher crashes before marking the Outbox row as published, the row may be sent again.

This duplicate is expected.

The producer must not try to solve this by marking the Outbox item published before Kafka acknowledges the record.

## Consumer boundary

For a successfully handled Kafka record:

```text
receive record
    |
    v
begin local DB transaction
    |
    +--> check Inbox
    +--> validate business state
    +--> apply business transition
    +--> write Inbox marker
    +--> write new Outbox messages
    |
    v
commit DB transaction
    |
    v
commit Kafka offset
```

If the process crashes after the DB commit but before the offset commit, Kafka may redeliver the record.

On redelivery, the Inbox entry turns the duplicate into a safe no-op.

The Inbox marker must participate in the same local transaction as the business mutation whenever that consumer changes persisted state.

## Idempotent no-op rule

A duplicate is not considered an error when the requested outcome has already been reached consistently.

Examples:

- `ConfirmOrder.v1` arrives for an already confirmed Order;
- `ConsumeInventory.v1` arrives for an already consumed reservation;
- `ReleaseInventory.v1` arrives for an already released reservation;
- a duplicate `PaymentCaptured.v1` event reaches Saga;
- the same provider webhook is received twice.

The handler returns/records an idempotent success or no-op.

A contradictory state is different from a duplicate and must not be silently accepted.

## Database constraints

Idempotency is enforced by both application logic and database constraints where possible.

Examples include unique constraints for:

- HTTP idempotency keys within the appropriate scope;
- `(ConsumerName, MessageId)` Inbox identity;
- one logical Payment identity;
- provider operation idempotency key;
- Refund identity;
- Reservation identity;
- Restock operation identity;
- ledger posting identity for a logical financial operation.

Database constraints are the final protection against concurrent duplicate execution.

## External payment provider rule

Provider calls require a deterministic idempotency key.

For example, a logical capture operation may derive a stable key from:

```text
payment:{PaymentId}:capture:v1
```

Retries after:

- request timeout;
- process restart;
- transient HTTP failure;
- reconciliation attempt

must reuse the same logical provider operation key.

Generating a new provider idempotency key for a retry is forbidden because it may create a second charge.

## Ambiguous outcome rule

A timeout does not prove that an external operation failed.

Example:

1. PayFlow sends capture request.
2. Provider captures successfully.
3. Response is lost.
4. PayFlow observes a timeout.

Payment remains in an unresolved/processing state until:

- retry with the same provider operation identity returns an authoritative result; or
- reconciliation queries the provider and determines the outcome.

The system must not emit `PaymentFailed.v1` merely because the original HTTP response was not received.

## Ledger rule

Financial ledger effects must be idempotent independently of message delivery.

A duplicate Payment command, event, webhook, or reconciliation result must not create another ledger posting for the same logical financial operation.

Ledger entries are append-only.

Corrections use explicit reversing transactions rather than deleting or rewriting posted history.

## HTTP idempotency

Public create-style endpoints that can produce durable side effects use an `Idempotency-Key` where required.

For Order creation:

- same key + same canonical request -> return the original logical result;
- same key + different canonical request -> reject as an idempotency conflict;
- the key and request hash are persisted durably;
- concurrent requests with the same key must not create two Orders.

HTTP idempotency is separate from Kafka Inbox deduplication.

## Alternatives considered

### End-to-end exactly-once claim

Rejected because the workflow crosses:

- PostgreSQL transactions;
- Kafka;
- multiple independent services;
- an external payment provider.

No single atomic transaction covers all of these boundaries.

Using the phrase "exactly once" for the full business workflow would hide real failure modes that still require idempotency and reconciliation.

### Kafka transactions / exactly-once semantics as the primary correctness mechanism

Deferred/rejected for the baseline because Kafka transactions cannot atomically include:

- service PostgreSQL state;
- external provider charges;
- provider webhooks.

Kafka transactional features may reduce some broker-level duplicates but do not remove the business-level idempotency requirements.

### At-most-once delivery

Rejected because committing offsets before durable business handling can lose messages after crashes.

For payments and stock workflows, silent loss is less acceptable than safe duplicate delivery.

### Deduplication using only Kafka offsets

Rejected because:

- offsets change on replay;
- a logical operation can be represented by another message instance;
- provider/webhook duplicates do not share Kafka offsets;
- business retries need stable operation identities.

## Consequences

Positive consequences:

- crash/restart scenarios are recoverable;
- duplicate delivery becomes an explicitly tested condition;
- provider retries are safe;
- DLQ replay can preserve business correctness;
- transport implementation is not confused with business guarantees;
- failure recovery is explainable during review and interviews.

Negative consequences:

- every side-effecting handler needs deliberate idempotency design;
- Inbox/Outbox tables add storage and cleanup concerns;
- unique constraints require careful scoping;
- duplicate/no-op outcomes need observability;
- reconciliation is required for ambiguous external operations;
- testing is more complex than a simple happy-path message flow.

## Implementation constraints

When implementation begins:

1. Kafka auto-commit is disabled for state-changing consumers;
2. a consumer commits an offset only after reaching a safe durable outcome;
3. Inbox deduplication uses `MessageId` plus consumer identity;
4. business idempotency uses a separate logical operation identity;
5. Outbox and business mutation share one local transaction;
6. Inbox and business mutation share one local transaction where applicable;
7. provider retries reuse deterministic idempotency keys;
8. financial effects have unique logical posting identities;
9. duplicate delivery is logged/metriced as a normal outcome;
10. handlers distinguish duplicate, contradiction, business failure, and technical failure.

## Testing implications

At minimum, tests must prove:

- duplicate `OrderCreated.v1` creates one Saga;
- duplicate reserve command does not reserve stock twice;
- duplicate consume command does not consume stock twice;
- duplicate release command does not release twice;
- duplicate restock command does not increase stock twice;
- duplicate capture command does not create a second provider charge;
- duplicate payment webhook does not create a second transition;
- duplicate payment success does not create a second ledger posting;
- crash after DB commit but before Kafka offset commit is safe;
- Outbox republish after publisher crash is safe;
- HTTP create-order retries return the same logical Order;
- same HTTP idempotency key with different payload is rejected;
- DLQ replay does not duplicate completed business effects;
- ambiguous provider timeout is reconciled rather than guessed as failure.

## Review trigger

Revisit this ADR only if the complete business workflow can genuinely be enclosed in a stronger atomic boundary or if a new infrastructure capability materially changes the tradeoff.

Even then, external side effects and replay behaviour must be evaluated separately before reducing idempotency protections.
