# ADR-002: Saga Orchestration over Choreography

## Status

Accepted.

## Context

PayFlow checkout is a multi-step distributed workflow involving independently owned bounded contexts:

1. Order enters processing.
2. Inventory is reserved.
3. Payment is captured.
4. Inventory reservation is consumed.
5. Order is confirmed.

Failure at later steps may require compensating earlier work.

Examples include:

- payment failure after inventory reservation;
- inventory commit failure after payment capture;
- permanent Order confirmation failure after payment capture and inventory consumption;
- process restart while a workflow is in progress;
- timeout where an external payment outcome is ambiguous;
- retry after a transient Kafka or database failure.

The workflow therefore needs durable progress tracking, explicit deadlines, idempotent compensation, and recovery after process restarts.

## Decision

PayFlow uses an **orchestrated Saga** for the checkout workflow.

A dedicated Saga service owns only workflow state and coordination metadata.

It does not own Order, Inventory, or Payment business state.

The Saga:

- consumes integration events from Order, Inventory, and Payment;
- persists the current workflow step;
- persists deadlines and retry/recovery metadata;
- sends commands to the owning bounded context;
- decides when compensation must begin;
- resumes from persisted state after restart;
- treats duplicate messages idempotently;
- does not directly mutate another service's database.

The owning service remains authoritative for every business transition.

For example:

```text
Saga -> CapturePayment.v1 -> Payment
Payment -> PaymentCaptured.v1 -> Saga
```

Saga requests the transition; Payment decides, persists, and publishes the authoritative result.

## Why orchestration fits this workflow

The checkout process has a clear business sequence and several non-trivial compensation paths.

An orchestrator gives one durable place to answer operational questions such as:

- Which step is this Order currently waiting for?
- Has Inventory already been reserved?
- Has Payment already been captured?
- Is compensation in progress?
- Which compensation step completed?
- When should the next retry occur?
- Has the workflow exceeded its operational deadline?
- Can this Saga safely resume after a worker restart?

This visibility is especially important for payment-related failure recovery.

## Alternatives considered

### Pure event choreography

In a choreography model, each service reacts to events and independently decides what to publish next.

Example:

```text
OrderCreated
  -> Inventory reacts
InventoryReserved
  -> Payment reacts
PaymentCaptured
  -> Order reacts
```

This was rejected for the core checkout workflow because:

- the end-to-end workflow becomes distributed across multiple services;
- compensation logic becomes harder to understand and test;
- ownership of deadlines and retry progression becomes ambiguous;
- diagnosing a stuck checkout requires reconstructing behaviour from many services;
- adding a new step can create hidden event coupling;
- failure paths such as refund + restock + cancellation become difficult to reason about as a single process.

Choreography is not forbidden globally.

It may still be appropriate for independent side effects such as future analytics, audit, or notifications where no central business workflow decision is required.

### Synchronous request chain

Another option would be for one HTTP request to synchronously call:

```text
Order -> Inventory -> Payment -> Order
```

This was rejected because:

- one slow dependency extends the full request latency;
- temporary downstream failure directly breaks the request chain;
- retries can accidentally duplicate financial operations;
- process crash loses in-memory progress;
- compensation and ambiguous payment outcomes are harder to recover safely;
- service availability becomes tightly coupled.

Synchronous APIs may still be used later for narrowly justified queries, but they are not the checkout workflow coordinator.

### In-memory workflow coordinator

A background worker could coordinate the workflow without persisting Saga state.

Rejected because process restart would lose:

- the current step;
- retry counters;
- deadlines;
- compensation progress.

A distributed workflow must not depend on one process remaining alive.

### External workflow engine

A dedicated workflow platform could provide durable orchestration.

This is deferred for the baseline because:

- the current workflow can be implemented with PostgreSQL-backed Saga state and Kafka;
- introducing another infrastructure platform would increase operational scope;
- PayFlow intentionally demonstrates the mechanics of Saga persistence, idempotency, compensation, and recovery.

An external workflow engine may be reconsidered if workflow count, duration, scheduling complexity, or operational requirements grow substantially.

## Consequences

Positive consequences:

- the checkout workflow is explicit and reviewable;
- Saga progress survives restarts;
- compensation paths are centralized and testable;
- operational debugging has a clear workflow record;
- deadlines and retries have a defined owner;
- business services remain authoritative for their own state;
- duplicate events can be reconciled against persisted Saga state.

Negative consequences:

- Saga becomes an important operational component;
- its state machine requires careful versioning;
- the orchestrator knows the sequence of several bounded contexts;
- bugs in Saga logic can affect many workflows;
- additional persistence and concurrency handling are required;
- long-running Saga migrations require care.

## Coupling rule

Saga may know:

- integration contracts;
- workflow sequence;
- compensation sequence;
- correlation identifiers;
- workflow deadlines.

Saga must not know or depend on:

- another service's EF entities;
- another service's database schema;
- another service's repositories;
- internal aggregate implementation details.

This keeps orchestration coupling at the contract level rather than the persistence/domain implementation level.

## Failure-handling rule

A technical failure does not automatically become a business failure.

Examples:

- Kafka unavailable;
- PostgreSQL temporarily unavailable;
- process restart;
- payment provider timeout with ambiguous outcome.

For these cases, Saga keeps the current business workflow state truthful and persists retry/deadline metadata.

Business compensation begins only when the workflow has a sufficiently authoritative outcome to justify it.

## Concurrency rule

Saga state is persisted with optimistic concurrency or equivalent database protection.

Two workers must not advance the same Saga through conflicting transitions.

Kafka partitioning by `OrderId` reduces concurrent handling but is not considered sufficient protection by itself.

## Implementation constraints

When Saga implementation begins:

1. every Saga instance is correlated by `OrderId`;
2. Saga state is stored in `saga_db`;
3. current step and version are persisted;
4. deadlines are persisted as timestamps;
5. retry/recovery metadata is persisted;
6. consumed message IDs are deduplicated;
7. outgoing commands are written through the local Outbox;
8. handlers validate legal state transitions;
9. process restart must resume the workflow without manual reconstruction;
10. compensation commands must be idempotent.

## Testing implications

At minimum, integration/E2E tests must cover:

- happy-path checkout;
- insufficient inventory;
- definitive payment failure;
- process crash and restart while waiting for Inventory;
- process crash and restart while waiting for Payment;
- duplicate integration event;
- payment captured then Inventory commit failure;
- payment captured and Inventory consumed then permanent Order confirmation failure;
- repeated compensation command;
- Saga optimistic-concurrency conflict;
- persisted deadline recovery after restart.

## Review trigger

Revisit this ADR if:

- workflows become numerous enough that Saga code becomes difficult to maintain;
- workflow durations become very long;
- complex scheduling becomes a dominant concern;
- business teams require visual workflow operations;
- an external workflow engine provides a clear operational advantage;
- a future workflow is naturally choreography-based and does not require centralized compensation.

The decision should be made per workflow, not by assuming that every event-driven interaction must use the same coordination style.
