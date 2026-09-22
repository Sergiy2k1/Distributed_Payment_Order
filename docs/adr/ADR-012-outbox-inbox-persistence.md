# ADR-012: Transactional Outbox and Inbox Persistence Strategy

## Status

Accepted for V1.

## Context

PayFlow uses PostgreSQL for local business state and Kafka for asynchronous integration.

A local database transaction and a Kafka publish cannot be committed atomically as one distributed transaction.

Without an Outbox, this failure is possible:

1. service commits business state;
2. process crashes before Kafka publish;
3. downstream services never learn about the committed change.

Publishing to Kafka before the database commit creates the opposite failure:

1. Kafka record is published;
2. local database transaction fails;
3. downstream services observe a fact that never became true locally.

Consumers have a similar problem:

1. consumer applies a local state transition;
2. local transaction commits;
3. process crashes before Kafka offset commit;
4. Kafka redelivers the same message.

Therefore reliable messaging requires explicit local persistence around both publication and consumption.

## Decision

PayFlow uses:

- a **Transactional Outbox** in every service that publishes integration messages;
- an **Inbox / ProcessedMessages** record in every state-changing Kafka consumer;
- at-least-once Kafka delivery;
- idempotent handlers;
- local database transactions as the atomic boundary.

No end-to-end distributed transaction or 2PC is used.

## Outbox transaction boundary

When a service changes business state and must publish an integration message, both operations are committed in the same local PostgreSQL transaction.

Conceptually:

```text
BEGIN

apply business state change
insert OutboxMessage

COMMIT
```

Only after commit does a background publisher send the Outbox record to Kafka.

Examples:

```text
Order -> Confirmed
+
Outbox(OrderConfirmed.v1)
```

```text
Payment -> Captured
+
Ledger posting
+
Outbox(PaymentCaptured.v1)
```

```text
Inventory -> Reserved
+
Outbox(InventoryReserved.v1)
```

## Outbox record

The baseline Outbox record contains at least:

- `OutboxMessageId`;
- `MessageId`;
- `MessageType`;
- `SchemaVersion`;
- `AggregateId`;
- `CorrelationId`;
- `CausationId`;
- `OccurredAtUtc`;
- `Destination`;
- serialized payload/envelope;
- `CreatedAtUtc`;
- `PublishedAtUtc` when completed;
- `AttemptCount`;
- `NextAttemptAtUtc`;
- `LastErrorCode`;
- claim/lease metadata if required by the publisher implementation.

The exact physical schema may differ by service, but the semantics remain consistent.

## Outbox ownership

Each service owns its own Outbox table inside its own database.

Examples:

```text
orders_db     -> Order Outbox
inventory_db  -> Inventory Outbox
payments_db   -> Payment Outbox
saga_db       -> Saga Outbox
```

There is no central shared Outbox database.

A central Outbox would couple service availability and violate database ownership.

## Outbox publisher

Each publishing service runs a background publisher that:

1. selects eligible unpublished Outbox rows;
2. claims a small batch safely;
3. publishes each message to Kafka;
4. waits for broker acknowledgement;
5. marks the row as published;
6. records retry metadata on failure.

Multiple publisher instances must not intentionally process the same row concurrently.

The implementation may use PostgreSQL row claiming such as:

```text
FOR UPDATE SKIP LOCKED
```

or another equivalent lease/claim strategy.

The exact SQL is an implementation detail, but concurrent publishers must remain safe.

## Why duplicate publication is still possible

Even with row claiming, this crash can happen:

1. Outbox publisher sends message to Kafka.
2. Kafka persists and acknowledges it.
3. publisher crashes before `PublishedAtUtc` is committed.
4. publisher restarts.
5. the same Outbox row is published again.

Therefore the Outbox provides reliable eventual publication, not exactly-once delivery.

Consumers must still be idempotent.

## Outbox ordering

PayFlow does not assume global Outbox ordering.

For messages belonging to the same checkout workflow:

- Kafka key remains `OrderId`;
- the publisher should preserve stable per-aggregate ordering where practical;
- correctness must still rely on state machines and idempotency.

A later Outbox row being published before an earlier row must not silently corrupt business state.

Handlers must reject or safely no-op illegal transitions.

## Outbox retry

Publication failures are technical failures.

The publisher records:

- attempt count;
- last error classification;
- next eligible attempt time.

Retries use bounded/exponential backoff with jitter.

A Kafka outage must not roll back already committed business state.

Instead, Outbox backlog grows until Kafka is available again.

This backlog must be observable.

## Outbox retention

Published Outbox rows are not deleted immediately.

V1 keeps them for a configurable operational retention period so incidents can be investigated.

Cleanup runs separately and must only remove rows that are safely published and older than the configured retention threshold.

Cleanup policy is operational configuration, not part of business correctness.

## Inbox purpose

Inbox/ProcessedMessages prevents the same Kafka message from applying the same local side effect more than once.

A baseline Inbox identity is:

```text
UNIQUE(ConsumerName, MessageId)
```

`ConsumerName` distinguishes independently processing handlers/groups.

`MessageId` identifies the published integration message.

## Consumer transaction boundary

For a state-changing consumer:

```text
receive Kafka record
        |
        v
BEGIN local DB transaction
        |
        +--> check Inbox
        +--> validate message and current state
        +--> apply local business transition
        +--> insert Inbox marker
        +--> insert new Outbox messages
        |
        v
COMMIT local DB transaction
        |
        v
commit Kafka offset
```

The Inbox marker and business mutation must commit atomically.

The consumer must not insert the Inbox marker before successful business handling in a separate transaction.

## Duplicate consumption

If `(ConsumerName, MessageId)` already exists, the message is a known duplicate.

Expected behaviour:

- do not repeat the business side effect;
- do not create a second Outbox message for the same applied transition;
- treat processing as successful/no-op;
- allow the Kafka offset to progress after the duplicate check reaches a safe durable outcome.

Duplicate detection is a normal outcome and should be observable.

## Inbox is not sufficient business idempotency

Inbox handles redelivery of the same `MessageId`.

It does not replace business idempotency.

A logically identical operation may arrive with a different `MessageId`.

Examples:

- duplicate `ReserveInventory.v1` with same `ReservationId`;
- retry `CapturePayment.v1` with same `PaymentId`;
- duplicate `RefundPayment.v1` with same `RefundId`.

Handlers still enforce stable business operation identities and database constraints.

## Offset commit rule

Kafka auto-commit is disabled for state-changing consumers.

An offset is committed only after:

1. local business processing is durably complete; or
2. the record has been safely moved to the appropriate DLQ/quarantine according to the consumer policy.

Logging an exception is not enough to commit an offset.

## Consumer crash scenario

Expected scenario:

1. Kafka record is received.
2. local DB transaction commits.
3. Inbox marker exists.
4. process crashes before offset commit.
5. Kafka redelivers the record.
6. Inbox detects the same `MessageId`.
7. handler becomes a no-op.
8. offset is committed safely.

This scenario must not duplicate stock, payment, ledger, Saga, or Order effects.

## Producer crash scenario

Expected scenario:

1. business state + Outbox commit.
2. process crashes before publication.
3. service restarts.
4. Outbox publisher finds the unpublished row.
5. Kafka publish succeeds.
6. row is marked published.

No business event is permanently lost.

## Kafka outage scenario

Expected scenario:

1. services continue committing local transactions where business rules allow;
2. Outbox rows remain unpublished;
3. publisher retries with backoff;
4. Outbox backlog/oldest-age metrics increase;
5. Kafka recovers;
6. publishers drain backlog;
7. downstream consumers process at-least-once.

The system must not require manual reconstruction of events after a normal Kafka outage.

## Poison-message interaction

Inbox is written only after a message has a safe processing outcome.

Malformed or unsupported records are not marked as successfully processed before DLQ/quarantine handling completes.

A poison record follows the Kafka topology/DLQ policy defined elsewhere.

## Serialization failure

Outbox payload creation should happen before the database transaction commits when possible so an unserializable integration contract does not leave a committed business state with an unusable Outbox row.

At minimum, the service must guarantee that the persisted Outbox envelope is valid according to the contract serializer before commit.

## Transaction size

Outbox and Inbox transactions must remain focused.

They must not include:

- Kafka network calls;
- external HTTP/provider calls;
- arbitrary sleeps;
- long-running background work.

External work happens outside the DB transaction.

The database transaction protects only local durable state.

## Alternatives considered

### Publish directly after DB commit without Outbox

Rejected because process crash between commit and publish can permanently lose an integration message.

### Publish to Kafka before DB commit

Rejected because downstream consumers may observe a business fact that later rolls back locally.

### Kafka transactions as the only solution

Rejected because Kafka transactions do not atomically include PostgreSQL business state.

They also do not solve external payment provider side effects.

### Distributed transaction / 2PC

Rejected due to infrastructure coupling and because PayFlow intentionally models eventual consistency and compensation.

### Inbox in Redis only

Rejected because correctness-critical deduplication must survive cache loss/expiry and remain transactionally coupled with local state.

### Rely only on Kafka offsets for deduplication

Rejected because offsets do not represent stable business/message identity across replay and crash scenarios.

## Database constraints

Expected protections include:

- unique Outbox `MessageId`;
- unique Inbox `(ConsumerName, MessageId)`;
- index for unpublished Outbox selection;
- index for `NextAttemptAtUtc`;
- index/partitioning/cleanup strategy when data volume grows.

The exact PostgreSQL index design will be finalized with schema and performance testing.

## Observability requirements

Outbox metrics must include:

- unpublished row count;
- oldest unpublished message age;
- publish attempt count;
- publish failure count;
- publish latency;
- batch size;
- cleanup count.

Inbox/consumer metrics must include:

- processed count;
- duplicate/no-op count;
- handler failure count;
- DB transaction failure count;
- offset commit failure count.

Logs/traces should include:

- `MessageId`;
- `MessageType`;
- `OrderId`;
- `CorrelationId`;
- `CausationId`;
- Outbox row identity;
- consumer name;
- topic/partition/offset where applicable.

## Required tests

At minimum, tests must prove:

- business state and Outbox commit atomically;
- rollback removes both business mutation and Outbox row;
- Outbox publisher restart publishes pending rows;
- crash after Kafka acknowledgement but before marking Outbox published may duplicate transport delivery but not business effect;
- two publisher instances safely claim work;
- Kafka outage preserves Outbox backlog;
- duplicate `MessageId` is processed once per consumer;
- crash after local DB commit but before offset commit is safe;
- Inbox marker is not committed when business transaction fails;
- one consumed message can atomically create a new Outbox message;
- cleanup does not remove unpublished rows.

## Consequences

Positive consequences:

- local business state and integration intent cannot diverge silently;
- Kafka outages are recoverable through backlog;
- consumer crash/redelivery is safe;
- replay semantics are explicit;
- correctness stays inside local ACID transactions;
- implementation demonstrates a production-grade messaging pattern.

Negative consequences:

- every participating service needs Outbox/Inbox infrastructure;
- extra tables and indexes add storage;
- background publishers and cleanup jobs add operational work;
- duplicates still exist and must be handled;
- schema/retention tuning is required as volume grows.

These costs are accepted because reliable asynchronous integration is core to PayFlow.

## Implementation constraints

When messaging implementation begins:

1. business mutation + Outbox share one local transaction;
2. consumer mutation + Inbox share one local transaction;
3. Kafka network calls never happen inside the business DB transaction;
4. Outbox publication is at-least-once;
5. Inbox identity is unique per consumer/message;
6. business idempotency remains separate from Inbox deduplication;
7. state-changing consumers disable auto-commit;
8. offset commit happens only after a safe durable outcome;
9. Outbox publisher supports crash/restart and multiple instances safely;
10. backlog, retries, duplicates, and cleanup are observable.

## Review trigger

Revisit this ADR if:

- message volume requires partitioned Outbox tables;
- Debezium/CDC becomes operationally preferable;
- Kafka transactional capabilities materially simplify a local boundary;
- publisher polling becomes a measured bottleneck.

Any replacement must preserve the same core guarantee:

**a committed local business state change must not lose its required integration message, and duplicate delivery must remain safe.**
