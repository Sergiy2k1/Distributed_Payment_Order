# Bounded Contexts and Ownership

## Status

Accepted as the initial architecture baseline for PayFlow.

This document defines service boundaries and data ownership before implementation begins. It is intentionally focused on responsibilities and coupling rules rather than classes, endpoints, or persistence mappings.

## Architecture rule

PayFlow follows **database per service** ownership.

A service may access only its own persistence directly. Cross-service reads, joins, foreign keys, and shared DbContext instances are not allowed.

Integration between bounded contexts must happen through explicit contracts:

- asynchronous commands and events;
- public HTTP APIs at the system edge;
- narrowly justified synchronous internal APIs only when a later use case requires them.

The platform assumes eventual consistency between bounded contexts.

## Runtime components

### Gateway

**Responsibility**

The Gateway is the public entry point into PayFlow.

It owns edge concerns such as:

- request routing;
- authentication integration;
- coarse-grained authorization;
- correlation and trace propagation;
- public rate limiting when introduced.

**Does not own**

- order state;
- inventory state;
- payment state;
- saga state;
- business rules.

The Gateway must not become a shared business layer.

---

### Order bounded context

**Owns**

- orders;
- order items;
- order lifecycle state;
- customer/order relationship;
- immutable unit-price snapshot stored with the order;
- order-level idempotency result for order creation.

**Primary invariants**

- an order owns its item snapshot;
- an accepted create-order idempotency key cannot create multiple orders;
- an order transition must follow the Order state machine;
- downstream services must not update Order persistence directly.

**Persistence**

Initial database: `orders_db`.

The Order service is the only component allowed to write to this database.

**Does not own**

- physical stock;
- stock reservations;
- payment provider operations;
- ledger entries;
- distributed workflow progress.

The source used to determine the server-side price before the snapshot is stored will be decided separately. Regardless of that decision, once an order is created the snapshot belongs to Order.

---

### Inventory bounded context

**Owns**

- stock quantities by SKU;
- reserved quantities;
- inventory reservations;
- reservation expiration;
- release of reservations;
- concurrency control required to prevent overselling.

**Primary invariants**

- available stock is derived from owned inventory state;
- reserved quantity must never exceed valid stock capacity;
- concurrent reservation attempts must not oversell;
- the same logical reservation command must be idempotent.

**Persistence**

Initial database: `inventory_db`.

Only Inventory may read or mutate its tables directly.

**Does not own**

- order lifecycle;
- customer data;
- prices;
- payment status;
- saga orchestration state.

Inventory learns only the minimum order information required to reserve or release stock.

---

### Payment bounded context

**Owns**

- payment state;
- payment attempts;
- provider operation identifiers and idempotency keys;
- refunds;
- payment reconciliation state;
- immutable double-entry ledger data.

**Primary invariants**

- a retry must not create a second logical charge;
- provider responses must be applied idempotently;
- duplicate messages or webhooks must not create duplicate payment transitions;
- every posted ledger transaction must balance;
- posted ledger entries are immutable and corrections are represented by new reversing entries.

**Persistence**

Initial database: `payments_db`.

Only Payment may write payment and ledger data.

**Does not own**

- order lifecycle;
- stock reservations;
- saga progress.

Other services must not infer payment truth from their own local copies. Payment remains the source of truth for payment processing state.

---

### Saga bounded context

**Owns**

- distributed checkout workflow state;
- current saga step;
- persisted deadlines;
- compensation progress;
- retry/recovery metadata required by the workflow;
- deduplication of messages consumed by the saga.

**Responsibility**

Saga coordinates the business process across Order, Inventory, and Payment.

It decides which command should be issued next based on persisted workflow state and received integration events.

**Persistence**

Initial database: `saga_db`.

Saga state must survive process restarts. Workflow correctness must not depend on in-memory timers or process-local state.

**Does not own**

- order aggregates;
- stock;
- payment records;
- ledger entries.

Saga coordinates owners; it does not replace them.

---

### Mock Payment Provider

The Mock Payment Provider represents an **external system**, not a PayFlow business bounded context.

It exists to make provider behaviour deterministic and testable.

It will later support scenarios such as:

- success;
- decline;
- timeout before processing;
- timeout after processing;
- transient provider failure;
- duplicate webhook;
- delayed webhook.

It must not access `payments_db` directly.

Any provider-side state required to simulate idempotency or delayed outcomes belongs to the mock provider itself and is isolated from PayFlow service databases.

---

## Shared BuildingBlocks boundary

`src/BuildingBlocks` is reserved for narrowly scoped technical capabilities that are genuinely cross-cutting.

Initially acceptable candidates include:

- integration message envelope primitives;
- messaging abstractions;
- observability and correlation helpers;
- technical serialization conventions.

The following must **not** be placed in BuildingBlocks:

- Order entities;
- Payment entities;
- Inventory entities;
- Saga entities;
- EF Core entity mappings;
- service repositories;
- service DbContext implementations;
- service-specific status enums merely to avoid duplication.

Sharing integration contracts is allowed. Sharing domain ownership is not.

## Ownership matrix

| Concern | Owner |
| --- | --- |
| Public request routing | Gateway |
| Order and order items | Order |
| Order unit-price snapshot | Order |
| Stock and reservations | Inventory |
| Payment processing state | Payment |
| Provider operation state | Payment |
| Refunds | Payment |
| Double-entry ledger | Payment |
| Checkout workflow progress | Saga |
| Compensation progress | Saga |
| External PSP simulation | Mock Payment Provider |
| Cross-cutting technical primitives | BuildingBlocks |

## Database boundaries

Initial persistence boundaries are:

| Component | Database |
| --- | --- |
| Order | `orders_db` |
| Inventory | `inventory_db` |
| Payment | `payments_db` |
| Saga | `saga_db` |

These are ownership boundaries, not only deployment conventions.

Forbidden examples:

- Order querying Inventory tables;
- Saga joining Order and Payment tables;
- Payment creating a foreign key to an Order table;
- a shared DbContext containing entities from multiple services;
- a shared repository package used to bypass service APIs.

If a service needs information owned elsewhere, that information must arrive through an explicit contract or a justified API.

## Consistency model

There is no distributed database transaction across PayFlow services.

The baseline model is:

- local ACID transaction inside one service;
- asynchronous integration between services;
- eventual consistency across service boundaries;
- at-least-once message delivery;
- idempotent consumers and operations;
- compensation for business workflows when a later step fails.

This means temporary cross-service disagreement is expected and must be recoverable.

## Integration-data rule

Integration messages should expose only what another bounded context requires.

They are not serialized copies of internal aggregates.

A service may evolve its internal domain model independently as long as its published integration contract remains compatible.

## Review rule

When adding a new table, command, event, endpoint, or background worker, answer these questions first:

1. Which bounded context owns this data or behaviour?
2. Which database is the source of truth?
3. Why does another service need to know about it?
4. Is the integration contract smaller than the internal aggregate?
5. Does the design introduce a hidden shared database or shared domain model?
6. Can duplicate delivery or process restart break the ownership invariant?

If ownership is unclear, implementation should stop until the boundary is decided.
