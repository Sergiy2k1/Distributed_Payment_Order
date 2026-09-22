# ADR-008: PostgreSQL Inventory Locking Strategy

## Status

Accepted for V1.

## Context

Inventory reservation is one of the correctness-critical parts of PayFlow.

The service must prevent overselling when many checkout workflows attempt to reserve the same SKU concurrently.

For a stock row such as:

```text
SkuId: A-100
OnHand: 10
Reserved: 0
```

100 concurrent reservation attempts must never result in more than 10 units being reserved successfully.

The invariant is conceptually:

```text
0 <= Reserved <= OnHand
```

and:

```text
Available = OnHand - Reserved
```

The challenge is that multiple transactions may read the same availability at nearly the same time.

Application-level checks without database concurrency control are insufficient.

Example of an unsafe flow:

1. transaction A reads Available = 1;
2. transaction B reads Available = 1;
3. both decide reservation is allowed;
4. both increment Reserved;
5. stock is oversold.

Inventory therefore needs a concurrency strategy enforced close to the authoritative PostgreSQL data.

## Decision

PayFlow V1 uses **pessimistic row locking with PostgreSQL `SELECT ... FOR UPDATE`** for stock rows involved in a reservation.

For a multi-SKU reservation, stock rows are locked in a deterministic order:

```text
ORDER BY SkuId
FOR UPDATE
```

Conceptually:

```text
BEGIN

load requested stock rows
ORDER BY SkuId
FOR UPDATE

validate all requested quantities

if any item cannot be reserved:
    reject reservation
    ROLLBACK / persist rejection as designed
else:
    increment Reserved for all items
    persist Reservation + ReservationItems
    persist Outbox event
    COMMIT
```

The reservation decision and stock mutation happen in the same local PostgreSQL transaction.

## Why deterministic lock order is required

A single reservation may contain multiple SKUs.

Without a stable lock order, two concurrent transactions can deadlock.

Example:

```text
Transaction A locks SKU-1
Transaction B locks SKU-2

Transaction A waits for SKU-2
Transaction B waits for SKU-1
```

To reduce this risk, every reservation locks rows in the same canonical order:

```text
SkuId ascending
```

The exact database collation/identifier representation must be stable so all callers produce the same ordering.

Deterministic ordering reduces deadlocks but does not make them impossible under every future query pattern.

The service must still handle PostgreSQL deadlock/serialization failures as transient technical failures.

## Reservation transaction boundary

The V1 reservation transaction must include:

- loading and locking all required stock rows;
- validating requested quantities;
- updating reserved quantities;
- creating or updating the Reservation aggregate;
- creating ReservationItem records;
- writing the corresponding local Outbox message.

These operations must commit atomically inside `inventory_db`.

The service must not:

1. lock stock;
2. commit;
3. create the reservation in another transaction.

That would create a correctness gap.

## All-or-nothing multi-item reservation

An Order reservation is atomic at the Inventory service boundary.

If an Order requests:

```text
SKU-A x2
SKU-B x3
SKU-C x1
```

and SKU-B is insufficient, Inventory must not keep partial reservation side effects for SKU-A or SKU-C.

The baseline outcome is:

```text
all items reserved
OR
reservation rejected
```

This avoids forcing Saga to compensate partial reservation item success.

Partial fulfillment may be introduced only as a separate domain requirement with its own state model.

## Idempotency interaction

Pessimistic locking does not replace idempotency.

`ReserveInventory.v1` uses a stable `ReservationId`.

Before creating a second logical reservation, Inventory must detect whether that reservation already exists.

Expected behaviour:

- same `ReservationId` + equivalent logical request + already Reserved -> return/idempotently publish the existing result;
- same `ReservationId` + already Rejected -> return the existing rejection;
- same `ReservationId` + conflicting payload -> reject as an idempotency conflict;
- duplicate Kafka delivery must not increment `Reserved` twice.

A unique database constraint on the logical reservation identity remains required.

## Release and consume concurrency

The same reservation must not be consumed and released concurrently.

The transition:

```text
Reserved -> Consumed
```

and:

```text
Reserved -> Released
```

are mutually exclusive.

The implementation must use database concurrency protection for the Reservation row and stock mutation.

For V1, the same general approach applies:

- lock the relevant Reservation;
- lock affected stock rows in deterministic SKU order when stock counters change;
- validate the current state;
- apply exactly one legal transition;
- commit state + Outbox atomically.

## Expiration concurrency

Reservation expiration may race with:

- `ConsumeInventory.v1`;
- `ReleaseInventory.v1`;
- delayed duplicate messages.

Expiration must not simply run:

```text
UPDATE every row with ExpiresAtUtc < now
```

without validating reservation state and protecting concurrent transitions.

Only a reservation still in `Reserved` may expire.

If another transaction already committed `Consumed` or `Released`, expiration becomes a no-op.

## Restock concurrency

`RestockInventory.v1` represents a new append-only stock adjustment after a previously consumed reservation.

It must use a unique `RestockOperationId`.

The restock transaction must ensure:

- the original reservation is `Consumed`;
- the same restock operation was not applied before;
- stock is increased exactly once;
- the original reservation state is not rewritten;
- Outbox publication is atomic with the adjustment.

Row locking may be used while modifying stock counters, but operation idempotency remains mandatory.

## Isolation level

V1 does not require globally raising every Inventory transaction to `SERIALIZABLE`.

The baseline uses explicit row locking inside focused transactions.

Reasons:

- row ownership is clear;
- contention is concentrated on affected SKUs;
- locking behaviour is explicit and teachable;
- a global isolation-level increase could cause unnecessary retries for unrelated operations.

The actual default PostgreSQL isolation level remains acceptable when the required rows are explicitly locked before validating mutable stock state.

If testing reveals anomalies not covered by row locking, the isolation decision must be revisited.

## Alternatives considered

### Optimistic concurrency using a Version column

Example:

```text
UPDATE sku_stock
SET reserved = reserved + @qty,
    version = version + 1
WHERE sku_id = @sku
  AND version = @expectedVersion
```

This can work well under low contention.

Deferred as the primary V1 strategy because:

- multi-SKU reservations require retrying the full reservation when any row conflicts;
- high contention may produce repeated optimistic retries;
- reasoning about the all-or-nothing multi-row reservation becomes more involved;
- explicit row locking provides a clearer first implementation for correctness demonstrations.

Optimistic concurrency may still be used on other Inventory entities such as the Reservation state itself where appropriate.

### Atomic conditional UPDATE

Example:

```text
UPDATE sku_stock
SET reserved = reserved + @quantity
WHERE sku_id = @sku
  AND on_hand - reserved >= @quantity
```

Then success is determined from the affected-row count.

This is a strong alternative and may outperform `FOR UPDATE` for single-SKU hot paths.

It is deferred as the V1 primary approach because multi-SKU all-or-nothing reservations still require careful transaction ordering and rollback semantics.

We will benchmark this approach later rather than assuming which strategy is faster.

### Serializable isolation

Rejected as the default strategy because it broadens concurrency control beyond the exact rows that require protection and may increase transaction retries.

It remains a valid tool if future invariants require predicate-level protection.

### Redis distributed lock

Rejected as the correctness mechanism.

The authoritative stock lives in PostgreSQL, so concurrency protection should be enforced at the same durable boundary.

A Redis lock would add:

- another failure boundary;
- lock expiry semantics;
- network partition concerns;
- continued need for database constraints.

This is also consistent with ADR-006.

### In-process lock / Semaphore

Rejected because multiple service replicas do not share process memory.

An in-process lock cannot protect stock across:

- different pods;
- different machines;
- process restart.

It may reduce local contention but cannot be the correctness boundary.

## Lock duration rule

Transactions holding stock locks must remain short.

While holding a stock row lock, Inventory must not perform:

- HTTP calls;
- Kafka network publication;
- external provider calls;
- long-running computation;
- arbitrary sleeps/retries.

The transaction performs only local database work.

Integration events are written to the Outbox and published after commit.

This minimizes contention on hot SKUs.

## Deadlock handling

Even with deterministic SKU ordering, PostgreSQL may report a deadlock or transient transaction failure as the system evolves.

The handler must:

- classify it as a technical/transient failure;
- roll back the transaction completely;
- retry using the same business `ReservationId`;
- apply bounded retry/backoff;
- emit metrics for deadlock/retry frequency.

A database deadlock is not an `InventoryReservationRejected.v1` business outcome.

## Lock timeout

V1 should configure/observe a bounded database lock wait rather than allowing a request to wait forever.

A lock timeout is a technical failure.

It must not be translated to "insufficient inventory".

The exact timeout value will be chosen during implementation/load testing rather than hard-coded in this ADR.

## Observability requirements

Inventory concurrency telemetry must include:

- reservation attempts;
- reservation successes;
- reservation business rejections;
- duplicate/idempotent reservation hits;
- lock wait duration;
- database deadlock count;
- lock-timeout count;
- transaction retry count;
- hot SKU identifiers in sanitized operational metrics/logs where safe;
- reservation processing latency.

Load tests should make lock contention visible rather than hiding it.

## Required concurrency tests

The baseline must include an integration test equivalent to:

```text
Initial:
SKU-X OnHand = 10
Reserved = 0

Run:
100 concurrent requests
each requests quantity = 1

Expected:
successful reserved quantity <= 10
final Reserved = 10
final Available = 0
no negative availability
no duplicate reservation side effects
```

Additional tests must cover:

- concurrent requests for multiple SKUs in different input order;
- reserve vs expire race;
- consume vs release race;
- duplicate reserve command during contention;
- restock duplicate during contention;
- transaction rollback after one item in a multi-SKU request is insufficient.

## Benchmark plan

After the correct V1 implementation exists, benchmark:

1. `SELECT ... FOR UPDATE`;
2. atomic conditional `UPDATE`;
3. optimistic version-based update where meaningful.

Measure:

- throughput;
- p50/p95/p99 latency;
- lock wait time;
- retry rate;
- deadlock rate;
- PostgreSQL CPU;
- behaviour under a hot-SKU workload;
- behaviour under mostly-disjoint SKU workloads.

A future ADR revision may change the strategy based on evidence.

Correctness tests must remain identical regardless of the chosen implementation.

## Consequences

Positive consequences:

- overselling protection is enforced near the source of truth;
- multi-item reservations are easy to reason about atomically;
- deterministic lock order reduces deadlock risk;
- database transactions define a clear correctness boundary;
- crash rollback is handled by PostgreSQL;
- behaviour is straightforward to demonstrate in concurrency tests.

Negative consequences:

- hot SKUs can create lock contention;
- one slow transaction can delay other reservations for the same stock rows;
- careless transaction scope can hurt throughput;
- explicit SQL/EF locking support requires infrastructure-level implementation;
- scaling service replicas does not remove database contention on the same SKU.

These tradeoffs are acceptable for V1 because correctness is the primary requirement.

## Implementation constraints

When Inventory implementation begins:

1. stock is authoritative in PostgreSQL;
2. reservation requests lock required stock rows before checking availability;
3. multi-SKU rows are locked in deterministic `SkuId` order;
4. stock validation and mutation occur in one transaction;
5. Reservation + ReservationItems + Outbox participate in that transaction;
6. external network calls are forbidden while stock locks are held;
7. duplicate `ReservationId` cannot mutate stock twice;
8. consume/release/expire transitions are concurrency-protected;
9. deadlock/lock timeout is treated as technical failure;
10. concurrency tests must prove the no-oversell invariant.

## Review trigger

Revisit this ADR when:

- load tests show unacceptable lock contention;
- a hot-SKU workload materially limits throughput;
- atomic conditional updates demonstrate a clear advantage;
- partial reservation semantics are introduced;
- Inventory storage is redesigned;
- a new invariant requires broader predicate-level isolation.

Any replacement strategy must continue to prove the same stock invariants under concurrency.
