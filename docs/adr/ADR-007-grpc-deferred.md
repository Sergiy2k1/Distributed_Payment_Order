# ADR-007: gRPC Intentionally Deferred

## Status

Accepted.

## Context

PayFlow is designed around an asynchronous distributed checkout workflow.

The core path already uses:

- Kafka commands and events;
- persisted Saga orchestration;
- Transactional Outbox;
- Inbox deduplication;
- retries;
- compensation;
- reconciliation.

gRPC could be introduced for service-to-service communication, but using it in the core checkout path from the beginning would create a second interaction model before there is a concrete requirement for synchronous low-latency calls.

The project goal is not to demonstrate every transport technology. Each protocol should exist because its semantics fit a real use case.

## Decision

gRPC is **not part of the V1 checkout workflow**.

The baseline communication model is:

- public/client-facing APIs -> HTTP/REST through the Gateway where appropriate;
- distributed state-changing checkout workflow -> Kafka commands/events;
- internal synchronous RPC -> deferred until a specific use case requires it.

No gRPC package, contract, server, or client is added to V1 merely for portfolio breadth.

## Why Kafka remains the core workflow transport

The checkout flow contains operations whose correctness must survive:

- temporary service unavailability;
- process restart;
- delayed processing;
- duplicate delivery;
- long-running compensation;
- provider timeouts;
- ambiguous external outcomes.

Kafka plus persisted Saga state is better aligned with those requirements than a synchronous request chain.

For example:

```text
Saga
  -> CapturePayment.v1
  -> Kafka
  -> Payment

Payment
  -> PaymentCaptured.v1
  -> Kafka
  -> Saga
```

If Payment is temporarily unavailable, the workflow does not need the original caller to remain connected.

## What gRPC would be appropriate for

gRPC may be introduced later for a synchronous internal operation when all of these are true:

1. the caller needs an immediate response;
2. the operation is not a long-running workflow step;
3. synchronous availability coupling is acceptable;
4. request/response semantics are clearer than an event;
5. low serialization overhead or strongly typed contracts materially help;
6. timeout and retry behaviour can be defined safely.

Potential examples include:

- an internal read-only lookup;
- a narrowly scoped metadata query;
- a high-volume synchronous query where HTTP/JSON overhead is measured to matter.

These examples are not commitments. A concrete requirement must exist first.

## What gRPC must not replace

If introduced later, gRPC must not silently replace:

- Saga persistence;
- Kafka commands/events for long-running state transitions;
- Outbox/Inbox guarantees;
- compensation logic;
- provider reconciliation;
- database ownership boundaries.

Using gRPC does not make a distributed operation atomic.

## Alternatives considered

### Use gRPC for all internal service communication

Rejected because it would make the checkout workflow synchronously coupled.

A chain such as:

```text
Saga -> Inventory -> Payment -> Order
```

would require downstream availability during each request and would still need durable recovery when a process crashes between steps.

The system would end up implementing persistent workflow state anyway.

### Use REST for all internal communication

Rejected as the default workflow mechanism for the same reason: synchronous HTTP request/response does not solve long-running recovery or compensation.

REST remains appropriate at system boundaries and for explicit synchronous APIs.

### Mix Kafka and gRPC from day one

Deferred because no V1 requirement currently benefits enough from synchronous RPC to justify:

- another contract format;
- another client/server stack;
- another retry/timeout policy;
- another observability path;
- additional local development configuration.

A mixed architecture is acceptable later when each protocol has a clear role.

## Failure-model consequence

A gRPC call introduces synchronous failure modes such as:

- deadline exceeded;
- connection failure;
- unavailable server;
- partial processing with lost response;
- retry duplication if the operation is not idempotent.

Therefore any future state-changing gRPC operation must still define:

- idempotency;
- timeout semantics;
- retry safety;
- correlation;
- authoritative outcome;
- recovery after ambiguous failure.

gRPC itself does not eliminate these distributed-system problems.

## Contract ownership

If gRPC is introduced later:

- the receiving bounded context owns the RPC semantics;
- protobuf contracts are integration contracts, not domain entities;
- generated contracts must not expose EF Core entities;
- service-internal persistence models remain private;
- breaking contract changes require explicit versioning/migration.

## Observability requirements

Any future gRPC path must propagate:

- trace context;
- correlation identifiers;
- service identity.

It must expose at minimum:

- request count;
- latency;
- deadline exceeded count;
- retry count;
- error status distribution.

Its traces must join the same end-to-end OpenTelemetry trace model used by HTTP and Kafka.

## Security requirements

Future gRPC service-to-service calls must use authenticated service identity.

The baseline direction is:

- OIDC/service credentials where applicable;
- least-privilege authorization;
- TLS in deployed environments;
- no trust based only on internal network location.

The exact mechanism will be decided with the authentication/deployment design.

## Consequences

Positive consequences:

- fewer moving parts in V1;
- one clear asynchronous model for checkout;
- less duplicated retry/recovery logic;
- no premature protobuf/API surface;
- future gRPC introduction can be justified by measurements or semantics.

Negative consequences:

- V1 does not demonstrate gRPC;
- some future synchronous queries may initially use HTTP or remain unimplemented;
- adding gRPC later requires contract, deployment, and observability work.

These costs are acceptable because protocol count is not itself an architecture quality metric.

## Implementation constraints

Until this ADR is revisited:

1. core checkout state transitions remain Kafka-based;
2. no gRPC package is added without a concrete synchronous use case;
3. Saga must not depend on synchronous RPC to progress the baseline workflow;
4. service ownership is expressed through integration contracts, not shared code;
5. any temporary internal HTTP API must be explicitly justified rather than becoming a hidden synchronous workflow chain.

## Evidence required before adding gRPC

A proposal to add gRPC should document:

- the exact caller and owner;
- why synchronous response is required;
- expected request volume and latency target;
- why HTTP/REST is insufficient;
- failure and timeout semantics;
- idempotency requirements;
- retry policy;
- authentication/authorization model;
- observability plan;
- effect on availability coupling.

## Review trigger

Revisit this ADR when PayFlow has a concrete internal synchronous use case where gRPC provides a measurable or semantic advantage.

gRPC should be added because the interaction requires RPC semantics, not because it is a common item in backend technology lists.
