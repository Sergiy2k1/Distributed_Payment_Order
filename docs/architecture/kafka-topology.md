# Kafka Topology

## Status

Accepted as the initial Kafka transport topology for PayFlow.

The logical contracts are defined in `integration-contracts.md`. This document maps those contracts onto Kafka topics and defines partitioning, consumer groups, delivery semantics, offset rules, retry behaviour, dead-letter handling, and replay.

## Transport baseline

Kafka is the asynchronous integration transport between the core PayFlow bounded contexts.

The baseline topology uses:

- domain event topics owned by the producing service;
- command topics owned by the receiving service;
- explicit consumer groups;
- `OrderId` as the checkout partition key;
- manual offset control;
- transactional outbox at producers;
- inbox/idempotency at consumers;
- at-least-once delivery semantics;
- dedicated DLQ topics per consumer boundary.

Kafka transport guarantees are not treated as business exactly-once guarantees.

## Main topics

| Topic | Producer | Primary consumer group | Purpose |
| --- | --- | --- | --- |
| `orders.events` | Order | `payflow.saga.checkout.v1` | Facts owned by Order |
| `inventory.events` | Inventory | `payflow.saga.checkout.v1` | Facts owned by Inventory |
| `payments.events` | Payment | `payflow.saga.checkout.v1` | Facts owned by Payment |
| `orders.commands` | Saga | `payflow.order.commands.v1` | Commands directed to Order |
| `inventory.commands` | Saga | `payflow.inventory.commands.v1` | Commands directed to Inventory |
| `payments.commands` | Saga | `payflow.payment.commands.v1` | Commands directed to Payment |

Topic names intentionally do not contain a contract schema version.

Contract versions live in `MessageType` and `SchemaVersion`. This allows a topic to carry compatible `.v1` and future `.v2` contracts during migration without coupling transport names to every schema change.

## Why commands have separate owner topics

A single generic topic such as `saga.commands` is deliberately not used.

With a shared command topic:

- Order would receive Inventory and Payment commands it does not own;
- each consumer would need filtering logic for unrelated traffic;
- scaling and lag would be coupled across unrelated services;
- authorization and operational ownership would be less explicit.

Dedicated command topics make the destination owner obvious:

```text
orders.commands     -> Order
inventory.commands  -> Inventory
payments.commands   -> Payment
```

Saga is an orchestrator and therefore produces commands to owners rather than owning a generic command bus.

## Event fan-out

Event topics are owner-oriented rather than consumer-oriented.

For example:

```text
payments.events
    |
    +--> payflow.saga.checkout.v1
    |
    +--> future-audit-consumer
    |
    +--> future-notification-consumer
```

A future independent consumer receives the same event topic through its own consumer group.

Consumers must not share a group when they require independent copies of every event.

## Contract-to-topic mapping

### `orders.events`

Initial contracts include:

- `OrderCreated.v1`
- `OrderProcessingStarted.v1`
- `OrderConfirmed.v1`
- `OrderCancellationStarted.v1` when enabled
- `OrderCancelled.v1`

Future Order-owned events may be added while preserving compatibility rules.

### `inventory.events`

Initial contracts include:

- `InventoryReserved.v1`
- `InventoryReservationRejected.v1`
- `InventoryConsumed.v1`
- `InventoryReleased.v1`
- `InventoryRestocked.v1`

### `payments.events`

Initial contracts include:

- `PaymentCaptured.v1`
- `PaymentFailed.v1`
- `PaymentRefunded.v1`
- `PaymentRefundRejected.v1`

### `orders.commands`

Initial contracts include:

- `BeginOrderProcessing.v1`
- `ConfirmOrder.v1`
- `CancelOrder.v1`

### `inventory.commands`

Initial contracts include:

- `ReserveInventory.v1`
- `ConsumeInventory.v1`
- `ReleaseInventory.v1`
- `RestockInventory.v1`

### `payments.commands`

Initial contracts include:

- `CapturePayment.v1`
- `RefundPayment.v1`

## Partition key

For all checkout-related commands and events:

```text
Kafka key = OrderId
```

The serialized Kafka key must use one stable canonical representation of the Order identifier.

The key must not be randomly regenerated for retries or compensation messages.

### Why `OrderId`

The checkout workflow is correlated around one Order.

Using `OrderId`:

- keeps messages for the same Order in one partition within a given topic;
- reduces concurrent handling of the same workflow identity;
- makes traces, lag investigation, and replay easier;
- aligns with the Saga correlation key.

Identifiers such as `PaymentId`, `ReservationId`, and `RefundId` remain business idempotency identities inside payloads, but the checkout Kafka partition key remains `OrderId`.

## Ordering guarantee

Kafka ordering is relied on only within:

```text
one topic + one partition
```

PayFlow does **not** assume:

- global ordering across Kafka;
- ordering across different topics;
- ordering across different partitions;
- that the same `OrderId` is assigned the same partition number in two different topics;
- that retries or replays arrive before later unrelated records.

Even when all messages use `OrderId`, there is no cross-topic total order.

Correctness therefore depends on:

- persisted state machines;
- idempotent handlers;
- inbox deduplication;
- causation/correlation metadata;
- optimistic concurrency;
- explicit validation of legal transitions.

Kafka ordering is an optimization and simplification, not the only correctness mechanism.

## Initial partition count

Initial development topology:

```text
3 partitions per main topic
```

This is enough to exercise:

- multiple consumer instances;
- partition assignment;
- consumer-group rebalancing;
- per-key ordering;
- concurrent workflows.

The production partition count will be selected after load testing.

### Partition-count change warning

Increasing a topic's partition count can change the partition selected for a given key for future records.

Therefore partition count must not be changed casually while long-running workflows depend on key ordering.

Any production partition expansion requires an operational migration plan and verification that workflow correctness does not rely on old/new records staying in one physical partition across the change.

## Consumer groups

### Saga

Consumer group:

```text
payflow.saga.checkout.v1
```

Subscriptions:

- `orders.events`
- `inventory.events`
- `payments.events`

Multiple Saga Worker replicas use the same group and divide partitions between themselves.

A single Saga workflow is protected by persisted Saga concurrency even if records are duplicated or delivered around a rebalance.

### Order

Consumer group:

```text
payflow.order.commands.v1
```

Subscription:

- `orders.commands`

### Inventory

Consumer group:

```text
payflow.inventory.commands.v1
```

Subscription:

- `inventory.commands`

### Payment

Consumer group:

```text
payflow.payment.commands.v1
```

Subscription:

- `payments.commands`

## Consumer scaling

Within one consumer group, useful parallelism is limited by the number of assigned partitions.

For a topic with 3 partitions:

- one replica can process all 3;
- two replicas divide the partitions;
- three replicas can each own at least one partition;
- a fourth replica may remain idle for that topic.

Adding replicas does not create additional ordering domains.

Partition count is a capacity decision, not merely a deployment replica count.

## Delivery semantics

The platform assumes:

```text
at-least-once delivery
```

Duplicate delivery is expected during:

- consumer restart;
- rebalance;
- network interruption;
- crash after local database commit but before Kafka offset commit;
- outbox publish acknowledgement ambiguity;
- DLQ replay.

Business correctness must remain unchanged under duplicates.

## Producer rules

Application code does not publish an integration message directly as the final step of a business transaction.

The intended boundary is:

```text
local business state
       +
OutboxMessage
       |
       | same local database transaction
       v
COMMIT
       |
       v
Outbox publisher
       |
       v
Kafka
```

The Kafka producer should use idempotent-producer settings where supported, including strong acknowledgement settings in production.

However Kafka producer idempotence does not remove the need for consumer idempotency.

Example failure:

1. Outbox publisher sends a record.
2. Kafka persists it.
3. Publisher crashes before marking the Outbox row as published.
4. Publisher restarts and sends the row again.

The same business event can therefore still be delivered more than once.

## Consumer transaction boundary

Auto-commit is disabled.

For a normal successfully processed message, the intended sequence is:

```text
receive Kafka record
        |
        v
begin local DB transaction
        |
        +--> validate state
        +--> apply local transition
        +--> write Inbox/ProcessedMessage marker
        +--> write local Outbox messages if needed
        |
        v
commit local DB transaction
        |
        v
commit Kafka offset
```

If the process crashes after the database commit and before offset commit, Kafka redelivers the record.

The Inbox/idempotency check must then turn the duplicate into a safe no-op.

An Inbox record must not mark a message successfully processed before its business transaction succeeds.

## Offset commit rule

An input offset may be committed only when one of these conditions is true:

1. the business handling transaction completed successfully; or
2. the record was safely copied to its DLQ and the failure metadata required for investigation was persisted/published.

Offsets are not committed merely because an exception was logged.

## Retry strategy

### V1 decision

V1 deliberately does **not** introduce Kafka retry topics.

Transient consumer failures use:

- a small bounded immediate retry;
- exponential/jittered backoff;
- partition pause while the failing record is retried;
- no offset commit while the record is unresolved;
- persisted delivery-attempt/failure metadata when the service database is available;
- DLQ after the configured retry policy determines that automated retry is no longer appropriate.

This preserves the simplest ordering model for the initial financial workflow.

### Why retry topics are deferred

Delayed retry topics introduce additional concerns:

- records move to a different topic;
- original-topic ordering is no longer preserved;
- delayed delivery requires scheduling semantics that Kafka does not provide by itself;
- retry consumers and headers become another transport workflow;
- the same Order can have records simultaneously present in main and retry topics.

PayFlow will add retry topics only if load and chaos testing demonstrate that temporary partition blocking is an unacceptable operational limitation.

That decision must be recorded separately.

### Partition blocking tradeoff

The V1 strategy means one repeatedly failing record can temporarily block later records in the same partition.

This is accepted initially because:

- state-changing financial messages favor correctness and explainability;
- retries are bounded;
- poison records are eventually dead-lettered;
- the behaviour is visible through consumer lag metrics;
- later retry-topic evolution remains possible.

## Failure classification

### Business outcome

Examples:

- insufficient inventory;
- definitive payment decline.

Action:

- persist the business outcome;
- publish the corresponding business event;
- commit the offset.

Business failures are **not** sent to DLQ.

### Transient technical failure

Examples:

- temporary PostgreSQL connectivity failure;
- temporary Kafka publish failure;
- temporary provider/network problem when handled inside the owning service.

Action:

- retry according to policy;
- keep the current business state truthful;
- do not commit the input offset until handling reaches a safe outcome.

### Poison or unsupported message

Examples:

- malformed envelope;
- unsupported schema version;
- payload cannot be deserialized;
- permanently invalid required metadata.

Action:

- route to DLQ with sanitized diagnostics;
- commit the original offset only after DLQ publication succeeds.

### State contradiction

A message that cannot legally apply to the persisted state is not silently forced through.

Depending on whether it is a harmless late duplicate or a genuine contradiction:

- known duplicate/older fact -> idempotent no-op;
- unresolved contradiction -> DLQ/quarantine with state diagnostics.

## DLQ topics

Initial DLQ topology is consumer-boundary oriented:

| DLQ topic | Failed consumer boundary |
| --- | --- |
| `saga.events.dlq` | Saga consuming Order/Inventory/Payment events |
| `orders.commands.dlq` | Order consuming Order commands |
| `inventory.commands.dlq` | Inventory consuming Inventory commands |
| `payments.commands.dlq` | Payment consuming Payment commands |

A DLQ is not a replacement for business compensation.

It is an operational quarantine for records that automated processing cannot safely complete.

## DLQ metadata

A dead-letter record must preserve the original envelope and add transport diagnostics such as:

- `OriginalTopic`
- `OriginalPartition`
- `OriginalOffset`
- `ConsumerGroup`
- `AttemptCount`
- `FailureCategory`
- `FailureCode`
- `FailedAtUtc`
- `OriginalMessageId`
- sanitized error summary.

Raw secrets, credentials, payment secrets, or unrestricted stack dumps must not be copied into Kafka headers.

## DLQ replay

Replay is an explicit operator action.

A replay:

1. inspects the DLQ record and root cause;
2. verifies the code/config/data issue is corrected;
3. republishes the original logical envelope to the appropriate main topic;
4. preserves the original business identities;
5. preserves `MessageId` so successful prior processing remains deduplicatable;
6. adds replay audit metadata such as a new `ReplayId` and `ReplayedAtUtc`;
7. leaves the original DLQ record available for audit until retention removes it.

Replay is not implemented as an uncontrolled infinite loop.

## Rebalance behaviour

Consumer-group rebalances are normal runtime events.

Consumers must:

- stop accepting new work for revoked partitions;
- complete or cancel in-flight work according to the handler boundary;
- never commit offsets for records whose local transaction did not complete;
- tolerate the same record being delivered to another replica after rebalance.

Process-local locks are not sufficient protection for Saga, Order, Inventory, or Payment state.

Database-level concurrency/idempotency remains authoritative.

## Topic creation baseline

Initial local-development settings:

| Setting | Main topics | DLQ topics |
| --- | ---: | ---: |
| Partitions | 3 | 3 |
| Replication factor | 1 | 1 |
| Retention | 7 days | 30 days |

These are development defaults, not production SLOs.

For a production-like multi-broker environment, the intended direction is:

- replication factor 3;
- strong producer acknowledgements;
- at least two in-sync replicas for acknowledged production writes;
- retention sized from recovery/replay requirements and measured traffic.

Exact production values will be finalized with deployment and load-test evidence.

## Message size rule

Integration events should remain small business contracts.

Large binary payloads, documents, or logs are not placed in Kafka messages.

If a future workflow needs a large object, the message carries an identifier/reference and ownership metadata rather than embedding the object.

## Observability requirements

At minimum, Kafka telemetry must expose:

- consumer lag by group/topic/partition;
- processed message count;
- duplicate/no-op count;
- retry count;
- DLQ count;
- processing latency;
- outbox backlog;
- oldest unpublished outbox age;
- rebalance count;
- handler failure count.

Logs and traces should include:

- `MessageId`
- `MessageType`
- `OrderId`
- `CorrelationId`
- `CausationId`
- topic;
- partition;
- offset;
- consumer group.

## Topology overview

```text
                         +----------------------+
                         |      Order Service   |
                         +----------------------+
                           ^                  |
                           | orders.commands  | orders.events
                           |                  v
+-------------------+    +------------------------+
| Inventory Service |    |      Saga Worker       |    +-------------------+
+-------------------+    +------------------------+    | Payment Service   |
   ^             |          ^              ^           +-------------------+
   |             |          |              |              ^             |
   |             |          |              |              |             |
inventory.   inventory.  inventory.     payments.      payments.     payments.
commands     events      events         events         commands       events
   |             |          |              |              |             |
   +-------------+----------+--------------+--------------+-------------+

All checkout records use Kafka key = OrderId.
```

## Non-goals of the baseline

The initial topology does not use:

- Kafka transactions for end-to-end exactly-once business semantics;
- one shared `saga.commands` topic;
- retry-topic chains;
- Schema Registry solely for technology demonstration;
- Kafka as a database or source of truth for domain state;
- cross-service request/reply over Kafka for ordinary queries.

Each of these may be reconsidered only with a concrete requirement and documented tradeoff.
