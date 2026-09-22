# ADR-001: Database per Service

## Status

Accepted.

## Context

PayFlow is a distributed system composed of independently owned bounded contexts: Order, Inventory, Payment, and Saga.

Each context has different invariants, failure modes, and lifecycle rules. Allowing one service to query or mutate another service's tables directly would create hidden coupling between deployments, schemas, migrations, and business rules.

A shared database would also make it easy to bypass explicit integration contracts and would undermine the purpose of service ownership.

## Decision

Each core service owns its own PostgreSQL database:

- Order -> `orders_db`
- Inventory -> `inventory_db`
- Payment -> `payments_db`
- Saga -> `saga_db`

A service may directly access only its own persistence.

The following are forbidden across service boundaries:

- cross-service SQL joins;
- cross-service foreign keys;
- shared DbContext instances;
- repositories that expose another service's tables;
- direct writes to another service's database;
- migrations that span multiple service databases.

Cross-service data exchange happens through explicit integration contracts or a narrowly justified API.

Distributed workflows use eventual consistency, idempotency, retries, and compensation rather than a distributed database transaction.

## Alternatives considered

### One shared PostgreSQL database

Rejected because it:

- couples service schemas and migrations;
- enables accidental cross-service joins;
- makes ownership ambiguous;
- increases the blast radius of schema changes;
- hides distributed-system boundaries behind local transactions.

### One database with separate schemas per service

Better than fully shared tables, but still rejected for the baseline because database-level access remains too easy to cross and operational independence is weaker.

It may be acceptable in a constrained deployment only if schema ownership is technically enforced, but it is not the PayFlow target architecture.

### Distributed transaction / two-phase commit

Rejected because PayFlow intentionally models partial failure and recovery through Saga, Outbox/Inbox, and compensation.

2PC would increase infrastructure coupling and would not teach or demonstrate the failure-recovery model this project is designed to implement.

## Consequences

Positive consequences:

- service ownership is explicit;
- schemas can evolve independently;
- failures are isolated more clearly;
- integration contracts become intentional;
- local ACID boundaries remain simple;
- architecture tests can detect forbidden project dependencies.

Negative consequences:

- cross-service consistency becomes eventual;
- reporting across services requires dedicated read models or aggregation;
- duplicate delivery and retries must be handled correctly;
- operational complexity is higher than in a monolith;
- some workflows require compensation instead of rollback.

## Implementation constraints

When implementation begins:

1. each service must have its own connection string and DbContext;
2. migrations must be scoped to the owning service;
3. no project may reference another service's Infrastructure layer;
4. no EF entity from one service may be reused by another service;
5. integration messages must not expose persistence entities;
6. tests must verify architectural boundaries.

## Review trigger

Revisit this ADR only if a concrete operational requirement proves that the cost of separate databases outweighs the ownership benefits.

Any exception must document:

- the exact use case;
- the affected bounded contexts;
- the consistency requirement;
- the coupling introduced;
- the migration path back to explicit ownership if needed.
