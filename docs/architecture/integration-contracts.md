# Integration Command and Event Catalog

## Status

Accepted as the initial logical integration contract catalog for PayFlow.

This document defines the messages exchanged between bounded contexts. It describes message intent, producer, consumer, idempotency identity, and minimum payload. Physical Kafka topic names, partitioning, retry topics, and DLQ topology are defined separately.

## Contract rules

All integration messages follow these rules:

- contracts are versioned independently from internal domain models;
- message delivery is assumed to be at-least-once;
- every consumer must be idempotent;
- commands express an intent directed at one owning bounded context;
- events describe an already persisted fact published by the owner;
- consumers must not mutate another service's database;
- a message must contain only the data required by the receiving bounded context;
- internal EF entities, aggregates, and service-specific persistence models are never serialized as integration contracts;
- breaking contract changes require a new version;
- retries reuse the same logical operation identity.

## Common message envelope

Every command and event is wrapped in a common envelope.

Required metadata:

| Field | Purpose |
| --- | --- |
| `MessageId` | Unique identity of this published message instance |
| `MessageType` | Stable logical contract name |
| `SchemaVersion` | Contract schema version |
| `OccurredAtUtc` | Time the message/fact was created |
| `CorrelationId` | End-to-end workflow correlation |
| `CausationId` | Message that caused this message |
| `AggregateId` | Primary business aggregate involved |
| `Producer` | Publishing service |
| `TraceParent` | Distributed tracing context when available |
| `Payload` | Versioned contract payload |

For the checkout workflow, `OrderId` is the primary correlation/business key.

`MessageId` is used for inbox deduplication. It is not a replacement for business idempotency keys.

## Order contracts

### Event: `OrderCreated.v1`

**Producer:** Order  
**Primary consumer:** Saga

Published only after the new Order and its local outbox record are committed.

Minimum payload:

- `OrderId`
- `CustomerId`
- `Currency`
- `TotalAmount`
- `Items[]`
  - `SkuId`
  - `Quantity`
  - `UnitPrice`

Important rules:

- item prices are the immutable Order snapshot;
- the original HTTP request body is not republished;
- duplicate delivery must create at most one checkout Saga.

---

### Command: `BeginOrderProcessing.v1`

**Producer:** Saga  
**Consumer:** Order

Intent: transition an Order from `Pending` to `Processing`.

Minimum payload:

- `OrderId`

Business idempotency identity:

- `OrderId`

Expected outcomes:

- `OrderProcessingStarted.v1`
- idempotent no-op when the Order is already `Processing` or has advanced consistently;
- explicit rejection if the current Order state contradicts the command.

---

### Event: `OrderProcessingStarted.v1`

**Producer:** Order  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`

This event allows Saga to issue the inventory reservation command only after Order has authoritatively entered `Processing`.

---

### Command: `ConfirmOrder.v1`

**Producer:** Saga  
**Consumer:** Order

Intent: transition `Processing -> Confirmed` after payment capture and inventory consumption are confirmed.

Minimum payload:

- `OrderId`

Business idempotency identity:

- `OrderId`

Expected outcome:

- `OrderConfirmed.v1`

Replaying the command for an already confirmed Order is a successful no-op.

---

### Event: `OrderConfirmed.v1`

**Producer:** Order  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ConfirmedAtUtc`

This is the authoritative success event for the Order state transition.

---

### Command: `CancelOrder.v1`

**Producer:** Saga  
**Consumer:** Order

Intent: complete checkout cancellation only after required compensation has reached a safe point.

Minimum payload:

- `OrderId`
- `ReasonCode`

The exact user-facing reason text is not an integration contract requirement.

Expected outcomes:

- `OrderCancellationStarted.v1`
- `OrderCancelled.v1`

The implementation may persist `Cancelling` and `Cancelled` in one local operation only if the state model and audit requirements remain explicit. The external contract still treats cancellation as an owned Order transition.

---

### Event: `OrderCancellationStarted.v1`

**Producer:** Order  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReasonCode`

This event is useful when cancellation itself later gains local asynchronous work. It may be omitted from the first executable workflow if cancellation is an atomic local transition.

---

### Event: `OrderCancelled.v1`

**Producer:** Order  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReasonCode`
- `CancelledAtUtc`

This is the authoritative terminal event for failed checkout.

## Inventory contracts

### Command: `ReserveInventory.v1`

**Producer:** Saga  
**Consumer:** Inventory

Intent: reserve stock for one Order.

Minimum payload:

- `OrderId`
- `ReservationId`
- `Items[]`
  - `SkuId`
  - `Quantity`
- `ExpiresAtUtc`

Business idempotency identity:

- `ReservationId`

Expected outcomes:

- `InventoryReserved.v1`
- `InventoryReservationRejected.v1`

A retry with the same `ReservationId` must never reserve stock twice.

---

### Event: `InventoryReserved.v1`

**Producer:** Inventory  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReservationId`
- `ReservedAtUtc`
- `ExpiresAtUtc`

The event means the Inventory owner committed the reservation.

---

### Event: `InventoryReservationRejected.v1`

**Producer:** Inventory  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReservationId`
- `ReasonCode`

Typical reason:

- insufficient stock.

This is a business failure, not a technical retry signal.

---

### Command: `ConsumeInventory.v1`

**Producer:** Saga  
**Consumer:** Inventory

Intent: convert a successful reservation into committed stock consumption after payment capture succeeds.

Minimum payload:

- `OrderId`
- `ReservationId`

Business idempotency identity:

- `ReservationId`

Expected outcome:

- `InventoryConsumed.v1`

The command is valid only for a matching `Reserved` reservation. Replaying it after `Consumed` is a successful no-op.

---

### Event: `InventoryConsumed.v1`

**Producer:** Inventory  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReservationId`
- `ConsumedAtUtc`

This event authorizes Saga to proceed to Order confirmation.

---

### Command: `ReleaseInventory.v1`

**Producer:** Saga  
**Consumer:** Inventory

Intent: release a still-held reservation during compensation.

Minimum payload:

- `OrderId`
- `ReservationId`
- `ReasonCode`

Business idempotency identity:

- `ReservationId`

Expected outcome:

- `InventoryReleased.v1`

This command is for `Reserved -> Released`. It must not be used for an already `Consumed` reservation.

---

### Event: `InventoryReleased.v1`

**Producer:** Inventory  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReservationId`
- `ReleasedAtUtc`
- `ReasonCode`

---

### Command: `RestockInventory.v1`

**Producer:** Saga  
**Consumer:** Inventory

Intent: restore stock through a new inventory adjustment after a reservation was already `Consumed` but the distributed checkout later required compensation.

Minimum payload:

- `OrderId`
- `ReservationId`
- `RestockOperationId`
- `ReasonCode`

Business idempotency identity:

- `RestockOperationId`

Expected outcome:

- `InventoryRestocked.v1`

Important rules:

- the original reservation remains `Consumed`;
- the restock is an append-only business adjustment;
- retrying the same `RestockOperationId` must not increase stock twice.

---

### Event: `InventoryRestocked.v1`

**Producer:** Inventory  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `ReservationId`
- `RestockOperationId`
- `RestockedAtUtc`

## Payment contracts

### Command: `CapturePayment.v1`

**Producer:** Saga  
**Consumer:** Payment

Intent: perform one logical immediate-capture operation.

Minimum payload:

- `OrderId`
- `PaymentId`
- `Amount`
- `Currency`

Business idempotency identity:

- `PaymentId`

Provider operation identity must be derived deterministically from the logical Payment operation and reused across retries.

Expected outcomes:

- `PaymentCaptured.v1`
- `PaymentFailed.v1`

A timeout is not itself a `PaymentFailed` event.

---

### Event: `PaymentCaptured.v1`

**Producer:** Payment  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `PaymentId`
- `Amount`
- `Currency`
- `CapturedAtUtc`

The event is published only after Payment persistence and the corresponding ledger effect are safely committed through the local transaction/outbox boundary.

Duplicate delivery must not create a second capture or ledger posting.

---

### Event: `PaymentFailed.v1`

**Producer:** Payment  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `PaymentId`
- `ReasonCode`

This event represents a definitive business/provider failure where Payment knows that capture did not succeed.

Ambiguous provider outcomes remain in Payment recovery/reconciliation and must not be converted to this event merely because an HTTP call timed out.

---

### Command: `RefundPayment.v1`

**Producer:** Saga  
**Consumer:** Payment

Intent: compensate a previously captured payment.

Minimum payload:

- `OrderId`
- `PaymentId`
- `RefundId`
- `Amount`
- `Currency`
- `ReasonCode`

Business idempotency identity:

- `RefundId`

Expected outcomes:

- `PaymentRefunded.v1`
- `PaymentRefundRejected.v1`
- no terminal event while the provider outcome is still ambiguous.

---

### Event: `PaymentRefunded.v1`

**Producer:** Payment  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `PaymentId`
- `RefundId`
- `Amount`
- `Currency`
- `RefundedAtUtc`

The event is published only after the refund state and reversing ledger transaction are committed.

---

### Event: `PaymentRefundRejected.v1`

**Producer:** Payment  
**Primary consumer:** Saga

Minimum payload:

- `OrderId`
- `PaymentId`
- `RefundId`
- `ReasonCode`

A technical timeout does not qualify as rejection.

## Checkout workflow contract sequence

### Happy path

```text
Order
  OrderCreated.v1
      |
      v
Saga
  BeginOrderProcessing.v1
      |
      v
Order
  OrderProcessingStarted.v1
      |
      v
Saga
  ReserveInventory.v1
      |
      v
Inventory
  InventoryReserved.v1
      |
      v
Saga
  CapturePayment.v1
      |
      v
Payment
  PaymentCaptured.v1
      |
      v
Saga
  ConsumeInventory.v1
      |
      v
Inventory
  InventoryConsumed.v1
      |
      v
Saga
  ConfirmOrder.v1
      |
      v
Order
  OrderConfirmed.v1
```

### Insufficient inventory

```text
ReserveInventory.v1
  -> InventoryReservationRejected.v1
  -> CancelOrder.v1
  -> OrderCancelled.v1
```

### Definitive payment failure

```text
PaymentFailed.v1
  -> ReleaseInventory.v1
  -> InventoryReleased.v1
  -> CancelOrder.v1
  -> OrderCancelled.v1
```

### Failure after payment capture but before inventory consumption

If `ConsumeInventory.v1` cannot reach a valid committed outcome and the workflow is determined to be unrecoverable:

```text
RefundPayment.v1
  -> PaymentRefunded.v1
  -> ReleaseInventory.v1
  -> InventoryReleased.v1
  -> CancelOrder.v1
  -> OrderCancelled.v1
```

### Permanent failure after inventory consumption

If payment is captured and inventory is already consumed, but Order confirmation is permanently impossible:

```text
RefundPayment.v1
  -> PaymentRefunded.v1
  -> RestockInventory.v1
  -> InventoryRestocked.v1
  -> CancelOrder.v1
  -> OrderCancelled.v1
```

Transient Order confirmation failures do not start compensation. Saga retries `ConfirmOrder.v1` with the same logical workflow identity first.

## Messages that are deliberately not integration contracts

The initial design does not publish messages such as:

- `OrderRowUpdated`;
- `PaymentEntityChanged`;
- `StockTableChanged`;
- generic `EntityCreated` or `EntityUpdated`;
- serialized EF entities;
- internal repository notifications.

Contracts describe business intent or facts, not persistence mechanics.

## Compatibility rules

For a `.v1` contract, compatible evolution may include:

- adding an optional field with a safe default;
- adding metadata consumers may ignore;
- relaxing a consumer requirement where old messages remain valid.

Breaking changes include:

- changing the meaning of an existing field;
- changing required field type or units;
- reusing an old message name for different semantics;
- removing data required by an existing consumer.

Breaking changes require a new contract version such as `.v2`.

## Open decisions intentionally deferred

This catalog does not yet decide:

- physical Kafka topic names;
- partition count;
- Kafka retention;
- retry topic layout;
- DLQ naming;
- serialization format implementation;
- Schema Registry usage.

Those are transport/topology decisions and will be documented separately.

The business contract names and ownership defined here should remain meaningful even if the transport changes.
