# ADR-006: Redis Intentionally Deferred

## Status

Accepted.

## Context

Redis is a common component in distributed systems and could be used for:

- caching;
- distributed locks;
- idempotency keys;
- rate limiting;
- short-lived workflow state;
- pub/sub;
- counters.

However, PayFlow already has PostgreSQL as the durable source of truth for business state and Kafka as the asynchronous integration transport.

Adding Redis before a concrete requirement exists would introduce:

- another infrastructure dependency;
- another failure mode;
- another consistency model;
- cache invalidation concerns;
- additional local-development and deployment configuration;
- more observability and operational work.

The project goal is to demonstrate justified production engineering rather than maximize the number of technologies used.

## Decision

Redis is **not part of the initial PayFlow architecture**.

The V1 baseline uses:

- PostgreSQL for durable business state;
- PostgreSQL for idempotency records that must survive restarts;
- PostgreSQL for Saga state and deadlines;
- PostgreSQL for Outbox and Inbox persistence;
- Kafka for asynchronous integration.

Redis may be introduced later only for a measured and explicit use case.

## Redis is not a source of truth

If Redis is added later, it must not become the authoritative store for:

- Order state;
- Inventory quantities;
- Inventory reservations;
- Payment state;
- Refund state;
- ledger entries;
- Saga progress;
- Outbox publication state;
- Inbox deduplication state required for correctness.

Loss or eviction of Redis data must not corrupt financial or inventory correctness.

## Idempotency decision

Durable business idempotency remains database-backed.

Examples include:

- HTTP create-order idempotency;
- Kafka Inbox deduplication;
- Payment operation identity;
- Refund identity;
- Inventory reservation identity;
- Inventory restock identity.

Redis-only idempotency is rejected for correctness-critical operations because:

- keys may expire;
- data may be evicted;
- failover or misconfiguration can lose entries;
- correctness would depend on cache retention policy;
- concurrent durable state changes still require database constraints.

A future Redis layer may accelerate idempotency lookups, but PostgreSQL remains authoritative.

## Distributed locking decision

Redis distributed locks are not part of the V1 correctness model.

Inventory overselling prevention will use PostgreSQL concurrency mechanisms close to the authoritative stock data.

Saga concurrency will use persisted database concurrency protection.

Reasons include:

- a lock separate from the source-of-truth database adds another failure boundary;
- lock expiry can occur while work is still in progress;
- network partitions can make lock ownership difficult to reason about;
- database constraints are still required even when a distributed lock exists.

Redis locks may be reconsidered for a non-authoritative coordination use case, but not as the sole protection of financial or stock invariants.

## Caching decision

V1 deliberately starts without application-level Redis caching.

This allows us to measure:

- actual query latency;
- database load;
- hot endpoints;
- repeated read patterns;
- cacheable data volatility.

Only after measurements identify a bottleneck should a cache be introduced.

This avoids designing invalidation rules for data that may not require caching.

## Potential future use cases

Redis may be justified later for cases such as:

### Gateway rate limiting

A distributed rate-limit counter may require shared state across multiple Gateway replicas.

Redis could be appropriate if:

- multiple Gateway instances are running;
- in-memory rate limiting is insufficient;
- the required rate-limit semantics are documented.

### Read-through caching

Redis could cache expensive read models if load testing proves PostgreSQL is the bottleneck.

Requirements would include:

- clear cache key ownership;
- TTL policy;
- invalidation strategy;
- stale-data tolerance;
- metrics for hit ratio and stale reads.

### Short-lived non-authoritative data

Examples might include:

- temporary UI-facing status hints;
- short-lived request throttling metadata;
- derived counters.

These must remain reconstructable from authoritative systems.

## Alternatives considered

### Add Redis from day one

Rejected because there is no current requirement that PostgreSQL and Kafka cannot satisfy.

Adding it immediately would create architectural breadth without proving a need.

### Use Redis for Inventory reservations

Rejected because Inventory reservations are business-critical durable state.

They must survive:

- service restart;
- Redis restart;
- failover;
- cache eviction;
- long-running workflow recovery.

PostgreSQL is the authoritative store.

### Use Redis for Saga state

Rejected because Saga progress and deadlines must be durable and queryable during recovery.

A cache-oriented store is not required for the baseline.

### Use Redis for Kafka Inbox deduplication

Rejected as the sole deduplication mechanism because Inbox correctness must remain durable beyond TTL and cache loss.

### Use Redis Pub/Sub instead of Kafka

Rejected because Redis Pub/Sub does not provide the durable replay and consumer-group model required by the PayFlow workflow.

Kafka remains the integration backbone.

## Consequences

Positive consequences:

- fewer moving parts in the first executable system;
- simpler local development;
- simpler deployment topology;
- fewer consistency models;
- correctness remains anchored to durable stores;
- later Redis adoption can be justified by measurements.

Negative consequences:

- some reads may be slower until caching is introduced;
- distributed rate limiting may initially be simpler or single-instance;
- PostgreSQL handles more idempotency and coordination queries;
- future Redis adoption may require an additional deployment and observability phase.

These costs are acceptable until evidence shows otherwise.

## Implementation constraints

Until this ADR is revisited:

1. business correctness must not depend on Redis;
2. no Redis client package is added to service projects;
3. no Redis container is required for the baseline local stack;
4. idempotency remains durable in PostgreSQL;
5. Inventory concurrency remains database-backed;
6. Saga state and deadlines remain in PostgreSQL;
7. caches, if added elsewhere, must not silently become sources of truth.

## Evidence required before adding Redis

A Redis proposal should include at least:

- the concrete use case;
- measured problem or requirement;
- expected benefit;
- authoritative source of truth;
- TTL/eviction behaviour;
- invalidation strategy where applicable;
- failure-mode analysis;
- observability plan;
- fallback behaviour when Redis is unavailable.

## Review trigger

Revisit this ADR when a real requirement appears, such as:

- distributed Gateway rate limiting;
- measured database read bottleneck;
- a clearly cacheable read model;
- a non-authoritative high-throughput counter;
- another workload for which Redis provides a demonstrable operational advantage.

Redis should be added because it solves a measured problem, not because it is commonly listed in distributed-system technology stacks.
