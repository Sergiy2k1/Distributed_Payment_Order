# ADR-005: Dedicated Command Topics per Owning Service

## Status

Accepted.

## Context

The PayFlow Saga sends commands to multiple bounded contexts:

- Order;
- Inventory;
- Payment.

One possible transport design is to publish every command to a single shared topic such as:

```text
saga.commands
```

Each service would then subscribe to that topic and inspect `MessageType` to decide whether a message belongs to it.

That approach is simple to describe, but it weakens service ownership and couples unrelated consumers to the same transport stream.

The alternative is to use a dedicated command topic per receiving bounded context.

## Decision

PayFlow uses dedicated Kafka command topics owned by the receiving service:

```text
orders.commands     -> Order
inventory.commands  -> Inventory
payments.commands   -> Payment
```

The Saga publishes each command directly to the topic of the service that owns the requested business transition.

Examples:

```text
BeginOrderProcessing.v1 -> orders.commands
ConfirmOrder.v1         -> orders.commands
CancelOrder.v1          -> orders.commands

ReserveInventory.v1     -> inventory.commands
ConsumeInventory.v1     -> inventory.commands
ReleaseInventory.v1     -> inventory.commands
RestockInventory.v1     -> inventory.commands

CapturePayment.v1       -> payments.commands
RefundPayment.v1        -> payments.commands
```

Each owning service uses its own consumer group:

```text
payflow.order.commands.v1
payflow.inventory.commands.v1
payflow.payment.commands.v1
```

## Why this fits PayFlow

Commands have a single intended owner.

A command is not a broadcast fact; it is a request for one bounded context to attempt a state transition that only that owner can authorize.

Dedicated command topics make that ownership explicit at the transport level.

They also keep unrelated traffic out of each service's command stream.

## Alternatives considered

### One shared `saga.commands` topic

Rejected because it would require every command consumer to receive messages for other services.

For example:

```text
saga.commands
  |- BeginOrderProcessing.v1
  |- ReserveInventory.v1
  |- CapturePayment.v1
  |- RefundPayment.v1
  |- CancelOrder.v1
```

Then Order, Inventory, and Payment would all see the same stream.

Problems include:

- consumers must filter unrelated messages;
- lag from one command category becomes harder to interpret;
- poison messages may affect unrelated consumers operationally;
- topic retention and scaling policy become shared concerns;
- ACLs and ownership are less explicit;
- schema compatibility becomes broader than necessary;
- a generic command topic encourages the Saga to become a central bus rather than an orchestrator talking to explicit owners.

### One topic per command type

Example:

```text
reserve-inventory.commands
capture-payment.commands
confirm-order.commands
```

Rejected for the baseline because it would create too many transport artifacts for the current number of contracts.

The topic count would grow with every command version or new workflow step.

Grouping commands by owning service gives enough isolation without topic explosion.

### Request/reply over HTTP for all commands

Rejected as the default checkout coordination model because:

- service availability becomes synchronously coupled;
- long-running retries are harder to recover after process restart;
- transient downstream failures extend or break request chains;
- the Saga would need additional durable retry state around HTTP anyway;
- Kafka already provides the asynchronous integration backbone for the workflow.

HTTP remains valid for public APIs and narrowly justified synchronous queries.

## Topic ownership

The receiving service owns the semantics of its command topic.

### Order

Topic:

```text
orders.commands
```

Order owns:

- accepted command contract versions;
- handler compatibility;
- DLQ policy for Order command consumption;
- command-processing metrics.

### Inventory

Topic:

```text
inventory.commands
```

Inventory owns the equivalent responsibilities for Inventory commands.

### Payment

Topic:

```text
payments.commands
```

Payment owns the equivalent responsibilities for Payment commands.

Saga is a producer of these commands but does not own the receiving service's processing semantics.

## Partition key

All checkout commands use:

```text
Kafka key = OrderId
```

This keeps commands for one checkout workflow ordered within a topic partition.

However, this ADR does not rely on Kafka partition ordering as the only correctness mechanism.

Owning services must still enforce:

- idempotency;
- legal state transitions;
- database constraints;
- optimistic concurrency where required.

## Consumer isolation

Dedicated command topics allow each service to scale independently.

For example:

- Payment command load can increase without forcing Order to consume the same stream;
- Inventory can change its consumer replica count without changing Payment consumption;
- lag can be measured per owning service;
- DLQ volume is attributable to one bounded context.

This makes operational ownership easier to observe.

## Security and authorization consequence

Dedicated topics make Kafka ACLs easier to express.

Conceptually:

- Saga may produce to `orders.commands`, `inventory.commands`, and `payments.commands`;
- Order consumes only `orders.commands`;
- Inventory consumes only `inventory.commands`;
- Payment consumes only `payments.commands`.

A consumer does not need permission to read command traffic owned by another service.

The exact ACL implementation is deferred to deployment/security work.

## DLQ consequence

Each command consumer has a dedicated DLQ boundary:

```text
orders.commands.dlq
inventory.commands.dlq
payments.commands.dlq
```

This keeps poison-message investigation scoped to the owning bounded context.

A business failure such as insufficient inventory or payment decline is still represented as a business event, not sent to DLQ.

## Contract evolution

Topic names do not contain the schema version.

For example, both compatible command versions may temporarily travel through:

```text
payments.commands
```

while `MessageType` and `SchemaVersion` distinguish contract versions.

If a breaking migration requires separate transport isolation, that should be decided explicitly rather than encoding every contract version into topic names by default.

## Consequences

Positive consequences:

- clear transport ownership;
- simpler consumer filtering;
- independent scaling;
- easier lag and failure attribution;
- narrower Kafka ACLs;
- service-specific DLQs;
- less coupling between unrelated command categories;
- command flow mirrors bounded-context ownership.

Negative consequences:

- more Kafka topics than a single shared bus;
- Saga must know the destination topic for each owner;
- topology configuration must be kept consistent across environments;
- introducing a new command-owning bounded context requires a new command topic.

These costs are acceptable because topic count remains small and ownership is materially clearer.

## Implementation constraints

When Kafka integration is implemented:

1. command routing is based on the owning bounded context;
2. Order must not consume `inventory.commands` or `payments.commands`;
3. Inventory must not consume `orders.commands` or `payments.commands`;
4. Payment must not consume `orders.commands` or `inventory.commands`;
5. command topics use `OrderId` as the checkout partition key;
6. each command consumer has its own consumer group;
7. each command consumer has its own DLQ boundary;
8. handlers still validate `MessageType` and `SchemaVersion`;
9. ownership must be reflected in metrics and tracing;
10. integration tests must verify that each command is routed only to its owning service.

## Review trigger

Revisit this ADR if:

- the number of command-owning bounded contexts grows substantially;
- transport administration becomes dominated by topic count;
- a platform-level routing layer provides a clear operational advantage;
- a new messaging technology changes the ownership tradeoff.

Any replacement must preserve explicit command ownership and avoid reintroducing hidden cross-service coupling.
