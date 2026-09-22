# ADR-013: Monorepo and Service Project Layout

## Status

Accepted for V1.

## Context

PayFlow contains multiple independently owned runtime components:

- Gateway;
- Order Service;
- Inventory Service;
- Payment Service;
- Saga Worker;
- Mock Payment Provider.

The repository must make bounded-context ownership visible in the physical code structure.

At the same time, the project is intentionally developed as one portfolio/research system, so splitting every service into a separate repository would add repository, CI, versioning, and local-development overhead before independent teams actually require it.

A monorepo is therefore useful, but only if the folder and project rules prevent it from turning into a distributed monolith with arbitrary project references.

## Decision

PayFlow uses a **single monorepo with independently deployable bounded-context projects**.

Repository-level structure:

```text
/
|- src/
|- tests/
|- deploy/
|- docs/
|- PayFlow.slnx
|- Directory.Build.props
|- Directory.Packages.props
|- global.json
```

The monorepo is an organizational choice.

It does **not** imply:

- shared databases;
- shared DbContext;
- shared domain entities;
- direct cross-service repository access;
- one deployment unit;
- one process.

Each runtime component remains independently buildable and deployable.

## Target source layout

The intended source structure is:

```text
src/
|- Gateway/
|  `- PayFlow.Gateway/
|
|- Services/
|  |- Order/
|  |  |- PayFlow.Order.Domain/
|  |  |- PayFlow.Order.Application/
|  |  |- PayFlow.Order.Infrastructure/
|  |  `- PayFlow.Order.Api/
|  |
|  |- Inventory/
|  |  |- PayFlow.Inventory.Domain/
|  |  |- PayFlow.Inventory.Application/
|  |  |- PayFlow.Inventory.Infrastructure/
|  |  `- PayFlow.Inventory.Api/
|  |
|  |- Payment/
|  |  |- PayFlow.Payment.Domain/
|  |  |- PayFlow.Payment.Application/
|  |  |- PayFlow.Payment.Infrastructure/
|  |  `- PayFlow.Payment.Api/
|  |
|  |- Saga/
|  |  |- PayFlow.Saga.Domain/
|  |  |- PayFlow.Saga.Application/
|  |  |- PayFlow.Saga.Infrastructure/
|  |  `- PayFlow.Saga.Worker/
|  |
|  `- MockPaymentProvider/
|     `- PayFlow.MockPaymentProvider.Api/
|
|- Contracts/
|  |- PayFlow.Contracts.Order/
|  |- PayFlow.Contracts.Inventory/
|  `- PayFlow.Contracts.Payment/
|
`- BuildingBlocks/
   |- PayFlow.BuildingBlocks.Messaging/
   |- PayFlow.BuildingBlocks.Observability/
   `- PayFlow.BuildingBlocks.Web/
```

This is the target direction, not a requirement to create every project in one commit.

Projects are introduced incrementally so each commit remains small and reviewable.

## Layer responsibilities

### Domain

The Domain project contains business rules owned by that bounded context.

Typical contents:

- aggregates;
- entities;
- value objects;
- domain services where genuinely required;
- domain exceptions/errors;
- domain invariants;
- domain events that remain internal to the bounded context.

Domain must not depend on:

- EF Core;
- ASP.NET Core;
- Kafka;
- HTTP clients;
- another service;
- Infrastructure;
- API/Worker host projects.

The Domain project should remain the least coupled layer.

## Application

Application coordinates use cases inside one bounded context.

Typical contents:

- commands/queries;
- handlers;
- application services;
- ports/interfaces required by use cases;
- transaction orchestration at the application boundary;
- validation;
- mapping from integration/API input into domain operations.

Application may depend on its own Domain.

Application must not reference another service's Domain, Application, Infrastructure, or API project.

Cross-service interaction happens through integration contracts and infrastructure adapters.

## Infrastructure

Infrastructure contains technical adapters for one bounded context.

Typical contents:

- EF Core DbContext and mappings;
- PostgreSQL repositories;
- migrations;
- Kafka producers/consumers;
- Outbox/Inbox persistence;
- provider HTTP clients;
- background publisher implementations;
- authentication/authorization adapters where service-specific.

Infrastructure may depend on its own Application and Domain.

Infrastructure is not shared across bounded contexts through direct project references.

If technical behavior is truly generic, the minimal reusable part may move into a BuildingBlocks package.

## API

API is the HTTP composition root for a bounded context.

It contains:

- ASP.NET Core host;
- endpoints/controllers;
- authentication wiring;
- dependency injection composition;
- health endpoints;
- OpenAPI configuration;
- middleware that belongs to the host.

API should be thin.

Business rules do not live in controllers/endpoints.

## Worker

A Worker is a non-HTTP composition root.

For Saga:

```text
PayFlow.Saga.Worker
```

hosts:

- Kafka consumers;
- reconciliation/deadline workers owned by Saga;
- dependency injection;
- health/telemetry host configuration.

Saga business state and transition rules remain outside the host project.

## Gateway

`PayFlow.Gateway` is a separate runtime component.

Its responsibilities include:

- external routing;
- edge authentication;
- coarse authorization;
- correlation/trace propagation;
- resilience/rate limiting where justified.

Gateway must not contain:

- Order business rules;
- Inventory business rules;
- Payment business rules;
- direct access to service databases.

## Mock Payment Provider

The Mock Payment Provider is an **external-system simulator**, not a business bounded context.

Therefore it does not require ceremonial Domain/Application/Infrastructure projects unless its implementation later becomes complex enough to justify them.

For V1, one focused API project is preferred:

```text
PayFlow.MockPaymentProvider.Api
```

It must still support deterministic provider behaviours required by tests.

## Integration contract projects

Cross-service message DTOs live in owner-specific contract projects:

```text
PayFlow.Contracts.Order
PayFlow.Contracts.Inventory
PayFlow.Contracts.Payment
```

Examples:

```text
PayFlow.Contracts.Order
  OrderCreated.v1
  BeginOrderProcessing.v1
  OrderConfirmed.v1

PayFlow.Contracts.Inventory
  ReserveInventory.v1
  InventoryReserved.v1
  InventoryReservationRejected.v1

PayFlow.Contracts.Payment
  CapturePayment.v1
  PaymentCaptured.v1
  PaymentFailed.v1
  RefundPayment.v1
  PaymentRefunded.v1
```

These projects contain integration contracts only.

They must not contain:

- EF Core entities;
- repositories;
- domain aggregate implementations;
- service business logic;
- mutable shared domain state.

The owning bounded context defines the meaning of its contracts.

Consumers may reference the contract assembly without referencing the owner's implementation projects.

## Why contracts are separated from Domain

A domain model and an integration contract evolve for different reasons.

For example:

```text
Payment aggregate
!=
PaymentCaptured.v1 message
```

Exposing Domain classes directly as Kafka contracts would couple internal refactoring to external consumers.

Explicit contract DTOs preserve that boundary.

## BuildingBlocks rule

`src/BuildingBlocks` contains only genuinely technical cross-cutting code.

Potential examples:

### Messaging

- common envelope primitives;
- producer/consumer abstractions;
- correlation metadata helpers;
- generic Outbox/Inbox technical primitives where they remain domain-neutral.

### Observability

- OpenTelemetry registration;
- standard metric/tag names;
- trace propagation helpers.

### Web

- Problem Details configuration;
- correlation middleware;
- common health/host helpers.

BuildingBlocks must **not** become a SharedKernel containing:

- Order entity;
- Payment entity;
- Inventory Reservation;
- domain statuses;
- business validation;
- service repositories.

If a type has business meaning for one bounded context, that context owns it.

## Dependency direction

For a standard API service:

```text
Api
 |
 +--> Application
 |      |
 |      `--> Domain
 |
 `--> Infrastructure
        |
        +--> Application
        `--> Domain
```

Forbidden:

```text
Domain -> Infrastructure
Domain -> Api
Application -> Api

Order.Domain -> Payment.Domain
Order.Infrastructure -> Inventory.Infrastructure
Payment.Api -> orders_db
```

No service may obtain another service's business state through direct project or database access.

## Composition-root exception

The API/Worker host may reference Infrastructure in order to register concrete adapters.

That reference exists only at the composition root.

It does not mean endpoint code should call EF repositories directly.

## Project-reference rule

Allowed references are explicit and narrow.

Examples:

```text
PayFlow.Order.Application
    -> PayFlow.Order.Domain

PayFlow.Order.Infrastructure
    -> PayFlow.Order.Application
    -> PayFlow.Order.Domain

PayFlow.Order.Api
    -> PayFlow.Order.Application
    -> PayFlow.Order.Infrastructure
```

Cross-service implementation reference is forbidden:

```text
PayFlow.Order.Application
    -X-> PayFlow.Payment.Application

PayFlow.Payment.Infrastructure
    -X-> PayFlow.Inventory.Infrastructure
```

Cross-service communication uses:

- Kafka integration contracts;
- explicitly justified HTTP/gRPC contracts in the future.

## Test layout

Tests mirror the system concerns rather than being placed into one generic test project.

Target direction:

```text
tests/
|- Unit/
|  |- PayFlow.Order.UnitTests/
|  |- PayFlow.Inventory.UnitTests/
|  |- PayFlow.Payment.UnitTests/
|  `- PayFlow.Saga.UnitTests/
|
|- Integration/
|  |- PayFlow.Order.IntegrationTests/
|  |- PayFlow.Inventory.IntegrationTests/
|  |- PayFlow.Payment.IntegrationTests/
|  `- PayFlow.Saga.IntegrationTests/
|
|- Contract/
|  `- PayFlow.ContractTests/
|
|- Architecture/
|  `- PayFlow.ArchitectureTests/
|
|- E2E/
|  `- PayFlow.E2ETests/
|
`- Load/
   `- k6/
```

Projects are created only when there are real tests to place in them.

## Unit tests

Unit tests focus on pure business behavior:

- aggregate invariants;
- state transitions;
- value objects;
- application logic that can be tested without real infrastructure.

They should remain fast and deterministic.

## Integration tests

Integration tests use real infrastructure through Testcontainers where practical.

Examples:

- PostgreSQL transaction/concurrency behavior;
- EF mappings and constraints;
- Outbox/Inbox;
- Kafka producer/consumer behavior;
- Payment provider HTTP integration;
- reconciliation persistence.

Mocks must not replace the infrastructure behavior that the test is specifically intended to verify.

## Contract tests

Contract tests verify integration-message compatibility and semantics.

They may validate:

- serialization;
- required fields;
- schema version;
- envelope metadata;
- backward-compatible deserialization where required.

They do not replace end-to-end behavior tests.

## Architecture tests

Architecture tests enforce repository rules automatically.

Examples:

- Domain cannot reference Infrastructure;
- one bounded context cannot reference another context's implementation assembly;
- BuildingBlocks cannot reference service projects;
- contracts cannot reference service implementation projects;
- API endpoints do not directly reference another service's Infrastructure.

Architecture rules should fail CI rather than exist only in documentation.

## E2E tests

E2E tests verify the complete distributed workflow.

Examples:

```text
Create Order
-> reserve inventory
-> capture payment
-> consume inventory
-> confirm order
```

and failure/compensation paths.

These tests are intentionally fewer than unit/integration tests because they are slower and more expensive.

## Load tests

Load tests live separately because they have different execution and reporting needs.

Initial focus:

- hot-SKU inventory contention;
- checkout throughput;
- Kafka backlog recovery;
- Outbox publisher throughput;
- Payment provider latency/failure injection.

## Deployment independence

Each runtime component gets its own deployable artifact.

Eventually:

```text
PayFlow.Gateway
PayFlow.Order.Api
PayFlow.Inventory.Api
PayFlow.Payment.Api
PayFlow.Saga.Worker
PayFlow.MockPaymentProvider.Api
```

Each production runtime component has:

- its own Docker image;
- configuration;
- health checks;
- resource limits;
- deployment manifest.

Being in one repository does not require deploying all services together.

## Database migration ownership

Migrations live with the Infrastructure project that owns the database.

Conceptually:

```text
PayFlow.Order.Infrastructure -> orders_db migrations
PayFlow.Inventory.Infrastructure -> inventory_db migrations
PayFlow.Payment.Infrastructure -> payments_db migrations
PayFlow.Saga.Infrastructure -> saga_db migrations
```

A service must not contain migrations for another service's database.

## Package management

Package versions remain centralized through:

```text
Directory.Packages.props
```

This reduces accidental version drift while keeping package references explicit in each project.

Central package versioning does not imply architectural coupling.

## Shared build configuration

Repository-wide compiler/analyzer rules remain in:

```text
Directory.Build.props
.editorconfig
```

Service-specific configuration belongs to the service project.

## Solution organization

`PayFlow.slnx` contains all active projects.

Solution folders should mirror the major repository areas:

```text
Gateway
Services
  Order
  Inventory
  Payment
  Saga
  MockPaymentProvider
Contracts
BuildingBlocks
Tests
```

The solution is a developer-navigation/build artifact, not an architectural dependency mechanism.

Project references remain the enforceable dependency graph.

## Incremental project creation

The target tree is created incrementally.

We intentionally do not generate every empty project at once.

Reasons:

- each commit remains reviewable;
- package choices happen when the project needs them;
- unnecessary layers can still be challenged;
- build failures are easier to isolate;
- learning remains tied to concrete use cases.

The implementation phase should therefore create one bounded-context skeleton at a time.

## Alternatives considered

### Separate repository per service from day one

Deferred.

Potential benefits:

- stronger physical isolation;
- independent permissions;
- independent repository release history.

Rejected for current scope because:

- one developer owns the full system;
- local coordinated changes are common;
- shared CI/deployment learning is easier in one repository;
- repository administration would add noise without independent teams.

Repository split remains possible later because runtime and database boundaries are already explicit.

### Single ASP.NET Core project for all services

Rejected because it hides bounded-context and deployment boundaries.

It would not demonstrate the distributed architecture the project is designed to explore.

### One shared Domain project

Rejected because business concepts from Order, Inventory, and Payment would become coupled through a shared model.

Similar names do not imply shared ownership.

### One shared Infrastructure project

Rejected because it encourages:

- shared DbContext;
- cross-service repository access;
- shared migrations;
- hidden database coupling.

Technical reuse should be extracted narrowly into BuildingBlocks instead.

### Full Clean Architecture ceremony for every executable

Rejected.

Mock Payment Provider and Gateway do not automatically need four projects each.

Layers exist only where they protect meaningful dependencies.

## Consequences

Positive consequences:

- bounded contexts are visible in the repository;
- services remain independently deployable;
- dependency direction is explicit;
- cross-service implementation references can be tested;
- integration contracts are separated from domain models;
- technical reuse is possible without a shared business model;
- monorepo keeps local development and portfolio review convenient.

Negative consequences:

- the repository contains many projects;
- solution/build time will grow;
- coordinated package upgrades can touch many projects;
- discipline and architecture tests are required to prevent illegal references;
- monorepo permissions are coarser than separate repositories.

These tradeoffs are acceptable for the current project.

## Implementation constraints

When project creation begins:

1. use the `PayFlow.<BoundedContext>.<Layer>` naming convention;
2. Domain must remain infrastructure-free;
3. no service references another service implementation project;
4. integration contracts remain explicit DTOs;
5. BuildingBlocks contains technical concerns only;
6. APIs/Workers are composition roots, not business-logic layers;
7. migrations remain with the owning service;
8. tests mirror the relevant bounded context/system concern;
9. architecture tests enforce dependency rules;
10. projects are added incrementally in small commits.

## Review trigger

Revisit this ADR if:

- services move to independently owned teams;
- repository size materially harms CI/developer productivity;
- deployment cadence requires hard repository separation;
- a bounded context is extracted into a different technology stack.

A future repository split must preserve the same logical ownership boundaries rather than introduce them after the fact.
