# ADR-009: Immutable Double-Entry Ledger

## Status

Accepted for V1.

## Context

PayFlow Payment owns financial state that must remain explainable under:

- duplicate commands;
- duplicate provider webhooks;
- retries after ambiguous timeouts;
- reconciliation;
- refunds;
- process crashes;
- manual operational investigation.

A mutable "current balance" or editable transaction history would make it difficult to prove what happened.

The ledger must preserve financial history rather than overwrite it.

## Decision

PayFlow uses an **immutable double-entry ledger** inside the Payment bounded context.

Every posted financial transaction contains two or more ledger entries whose totals balance.

For a single currency:

```text
sum(Debits) = sum(Credits)
```

Posted ledger entries are append-only.

They are never edited or deleted to "fix" history.

Corrections are represented by a new explicit reversal transaction.

## Ledger ownership

The ledger belongs to the Payment bounded context.

Only Payment may create ledger transactions and entries.

Other services may learn payment outcomes through integration events, but they do not directly write ledger records.

The ledger is persisted in `payments_db`.

## Core concepts

### LedgerAccount

Represents one accounting bucket.

Initial account examples may include:

- customer/clearing receivable;
- merchant payable;
- provider clearing;
- refund clearing.

The exact chart of accounts will be finalized during Payment implementation.

The important invariant is that account semantics are explicit and stable.

### LedgerTransaction

Represents one logical financial posting.

Examples:

- payment capture;
- refund;
- reversal/correction.

A LedgerTransaction owns:

- `LedgerTransactionId`;
- `OperationType`;
- `BusinessOperationId`;
- `Currency`;
- `OccurredAtUtc`;
- optional reference to the Payment/Refund;
- immutable posting metadata.

### LedgerEntry

Represents one debit or credit line.

A LedgerEntry owns:

- `LedgerEntryId`;
- `LedgerTransactionId`;
- `AccountId`;
- `Side` = Debit or Credit;
- `Amount`;
- `Currency`.

An amount must be positive.

The debit/credit side carries the accounting direction.

## Balance invariant

A ledger transaction is valid only if:

```text
sum(debit amounts) = sum(credit amounts)
```

for the same currency.

A transaction that does not balance must not be committed.

The balancing check is enforced in application/domain logic and additionally protected by database constraints/transaction boundaries where practical.

## Currency rule

V1 does not mix currencies inside one LedgerTransaction.

All entries in a single LedgerTransaction use the same currency.

Cross-currency accounting is outside V1 scope.

If multi-currency settlement is added later, FX conversion and realized/unrealized differences require a separate design.

## Capture posting

A successful logical payment capture creates one ledger transaction.

Conceptually:

```text
Payment Captured: 100 USD

Debit  Provider/Clearing Asset      100 USD
Credit Merchant/Settlement Payable 100 USD
```

The exact account names may evolve, but the transaction must remain balanced.

The ledger posting is committed atomically with the authoritative Payment state transition whenever both belong to the same local Payment transaction.

## Refund posting

A successful refund does not delete or rewrite the original capture posting.

It creates a new reversal-style financial transaction.

Conceptually:

```text
Refund: 100 USD

Debit  Merchant/Settlement Payable 100 USD
Credit Provider/Clearing Asset     100 USD
```

The refund ledger transaction references the logical refund operation and, where useful, the original capture transaction.

Historical truth remains visible:

```text
Capture transaction exists
+
Refund transaction exists
```

instead of pretending the capture never happened.

## Immutability rule

Once posted, the following are immutable:

- LedgerTransaction business identity;
- transaction type;
- currency;
- posted entries;
- debit/credit side;
- amount;
- account assignment.

Fixing an error requires a new compensating/reversal transaction.

Administrative tooling must not expose ordinary UPDATE/DELETE operations for posted entries.

## Idempotent posting identity

Every logical financial operation has a stable posting identity.

Examples:

- capture posting -> based on logical `PaymentId` / capture operation;
- refund posting -> based on `RefundId`.

A unique database constraint must prevent the same logical operation from producing two ledger transactions.

Conceptually:

```text
UNIQUE(OperationType, BusinessOperationId)
```

or an equivalent stable posting key.

The exact schema may differ, but duplicate delivery must not produce duplicate postings.

## Relationship with Payment state

The ledger is not a replacement for Payment state.

Payment state answers operational questions such as:

- is the payment Processing?
- Captured?
- Failed?
- RefundPending?
- Refunded?

The ledger answers accounting/history questions such as:

- what financial postings were recorded?
- which accounts were debited/credited?
- what reversal corrected the original posting?

Both are required.

## Transaction boundary

For a successful capture, the intended local transaction is:

```text
BEGIN

update Payment -> Captured
insert LedgerTransaction
insert balanced LedgerEntries
insert Outbox(PaymentCaptured.v1)

COMMIT
```

For a successful refund:

```text
BEGIN

update Refund -> Succeeded
update Payment -> Refunded
insert reversal LedgerTransaction
insert balanced LedgerEntries
insert Outbox(PaymentRefunded.v1)

COMMIT
```

The state transition, ledger effect, and corresponding Outbox message must not become independently committed partial results.

## Duplicate handling

The system must tolerate duplicate:

- `CapturePayment.v1`;
- provider success response;
- provider webhook;
- reconciliation result;
- `RefundPayment.v1`;
- refund webhook/result.

If the same logical financial operation was already posted:

- no second LedgerTransaction is created;
- no second LedgerEntry set is created;
- the existing business result is reused/idempotently returned.

## Reconciliation interaction

Reconciliation may discover that the provider completed an operation that PayFlow previously considered ambiguous.

Example:

1. capture request times out;
2. Payment remains `Processing`;
3. reconciliation queries provider;
4. provider confirms success.

Reconciliation then performs the normal authoritative success transition.

It must use the same logical posting identity as the original capture operation.

Reconciliation is not allowed to create a second financial posting merely because it discovered the result later.

## Failure atomicity

If ledger posting fails, the Payment success transition must not commit independently.

For example, PayFlow must not end in:

```text
Payment = Captured
Ledger = missing
```

because a ledger insert failed.

Likewise, a ledger capture posting must not commit while Payment remains non-captured.

The local database transaction protects this invariant.

## Alternatives considered

### Mutable balance table only

Rejected because a current balance does not explain the sequence of financial operations that produced it.

It makes audit, debugging, reconciliation, and reversals harder.

A derived balance may exist later, but immutable postings remain authoritative.

### Single-entry transaction history

Rejected because one-sided entries do not enforce the accounting conservation invariant.

Double-entry makes mismatched financial movement visible through an unbalanced transaction.

### Update the original capture on refund

Rejected because it destroys historical truth.

A refund is a new business event, not an edit to the past.

### Delete incorrect ledger rows

Rejected because deletion removes auditability and makes operational investigation unreliable.

Corrections use explicit reversal transactions.

### Event Sourcing for the whole Payment bounded context

Deferred.

An immutable ledger provides append-only financial history without requiring every Payment domain state change to be event sourced.

Full Event Sourcing would add projection, replay, schema-evolution, and operational complexity beyond the current need.

## Database constraints

The implementation should enforce as much as practical in PostgreSQL.

Expected protections include:

- positive entry amount;
- valid debit/credit side;
- currency presence;
- unique logical posting identity;
- immutable posted rows through application permissions/design;
- foreign key from LedgerEntry to LedgerTransaction.

The exact mechanism for validating debit total equals credit total may involve domain validation plus transactional persistence.

If implemented with deferred database constraints/triggers, that choice must be documented separately.

## Precision rule

Money is stored using exact decimal/numeric semantics, never binary floating point.

The exact PostgreSQL precision/scale will be chosen explicitly during schema design.

All ledger arithmetic must preserve exact monetary values supported by the configured currency rules.

## Observability requirements

Ledger telemetry should expose:

- capture posting count;
- refund posting count;
- duplicate posting prevented count;
- ledger posting failures;
- reconciliation-created authoritative postings;
- invariant violation attempts.

Logs/traces must correlate ledger activity with:

- `PaymentId`;
- `RefundId` when applicable;
- `OrderId`;
- `CorrelationId`;
- logical posting identity.

Sensitive financial data must not be unnecessarily logged.

## Required tests

At minimum, tests must prove:

- every capture transaction balances;
- every refund/reversal transaction balances;
- an unbalanced transaction is rejected;
- duplicate capture command creates one ledger transaction;
- duplicate provider webhook creates one ledger transaction;
- reconciliation after timeout creates at most one capture posting;
- duplicate refund creates one reversal transaction;
- refund does not mutate/delete original capture entries;
- posted ledger entries cannot be edited through normal application flow;
- capture state + ledger + Outbox commit atomically;
- refund state + reversal ledger + Outbox commit atomically;
- exact decimal arithmetic preserves expected values.

## Future extensions

Future versions may add:

- settlement batches;
- fees;
- platform commission;
- chargebacks;
- disputes;
- partial refunds;
- multiple refunds;
- multi-currency settlement;
- FX accounting;
- account balance projections.

Each extension must preserve:

- append-only history;
- balanced transactions;
- stable business posting identity;
- explicit reversal/correction semantics.

## Consequences

Positive consequences:

- financial history is auditable;
- duplicate delivery can be made financially idempotent;
- refunds preserve original capture history;
- accounting inconsistencies are easier to detect;
- reconciliation has a clear posting model;
- portfolio reviewers can see explicit financial invariants rather than CRUD-only payment records.

Negative consequences:

- more schema and domain complexity;
- account semantics must be designed carefully;
- financial operations require multi-row transactional writes;
- correcting errors requires reversal logic;
- partial refunds and fees need deliberate accounting design later.

These costs are justified because Payment correctness is a core purpose of PayFlow.

## Implementation constraints

When Payment implementation begins:

1. ledger lives only in `payments_db`;
2. posted ledger records are append-only;
3. every financial LedgerTransaction balances;
4. one LedgerTransaction contains one currency in V1;
5. successful capture has one stable posting identity;
6. successful refund has one stable posting identity;
7. duplicate processing cannot create duplicate postings;
8. Payment/Refund state + Ledger + Outbox share one local transaction;
9. corrections use new reversal transactions;
10. money uses exact decimal/numeric representation.

## Review trigger

Revisit this ADR when:

- partial refunds are introduced;
- provider fees/commission are modeled;
- settlement accounts become more detailed;
- multi-currency support is required;
- chargebacks/disputes are introduced;
- accounting/reporting requirements require a richer chart of accounts.

Any evolution must preserve immutable balanced history.
