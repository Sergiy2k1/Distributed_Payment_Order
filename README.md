# PayFlow — Distributed Payment & Order Platform

Senior+ .NET backend portfolio project focused on reliability, consistency, failure recovery, idempotency, observability, and explicit transaction boundaries in a distributed payment and order-processing system.

## Current status

The architecture foundation is complete and the first vertical slice — **Order Service** — is under active implementation.

Implemented so far:

- .NET 10 solution and centralized build/package configuration
- bounded-context and distributed-system architecture documentation
- ADRs for database-per-service, Saga orchestration, at-least-once delivery, idempotency, payment capture, Kafka topology, Redis/gRPC deferral, inventory locking, and related design decisions
- Order Domain aggregate with OrderId, CustomerId, Money, Sku, OrderItem, explicit state machine, timestamps, aggregate versioning, and domain events
- Order Application layer with CreateOrder command/handler plus repository, clock, and unit-of-work abstractions
- Order Infrastructure with EF Core 10, Npgsql/PostgreSQL, OrderDbContext, persistence mappings, optimistic concurrency, repository, and initial migration
- Order API with POST /orders
- local PostgreSQL through Docker Compose
- unit tests with xUnit v3
- integration-test project foundation with Testcontainers for PostgreSQL

## High-level architecture

~~~text
Client
  |
  v
Gateway
  |
  +--> Order Service ------> orders_db
  |
  +--> Inventory Service --> inventory_db
  |
  +--> Payment Service ----> payments_db
  |
  +--> Saga Worker --------> saga_db

Kafka provides asynchronous integration between bounded contexts.
~~~

Core runtime components planned for the platform:

- Gateway
- Order Service
- Inventory Service
- Payment Service
- Saga Worker
- Mock Payment Provider

Each business service owns its own database. There are no cross-service joins, shared EF entities, shared repositories, or shared DbContext instances.

## Order Service structure

~~~text
src/Services/Order/
├─ PayFlow.Order.Api
├─ PayFlow.Order.Application
├─ PayFlow.Order.Domain
└─ PayFlow.Order.Infrastructure

tests/
├─ Unit/
│  └─ PayFlow.Order.UnitTests
└─ Integration/
   └─ PayFlow.Order.IntegrationTests
~~~

Dependency direction:

~~~text
API
  |
  v
Application
  |
  v
Domain

Infrastructure
  |
  +--> implements Application ports
  +--> depends on Domain/Application
~~~

The Domain layer does not depend on EF Core, PostgreSQL, Kafka, ASP.NET Core, or infrastructure-specific types.

## Order persistence model

Current PostgreSQL schema:

~~~text
orders
├─ id
├─ customer_id
├─ status
├─ total_amount
├─ currency
├─ created_at_utc
├─ updated_at_utc
└─ version

order_items
├─ order_id
├─ position
├─ sku
├─ quantity
├─ unit_price_amount
└─ currency
~~~

Important persistence rules:

- orders.id is client-generated
- (order_id, position) is the composite key for order items
- order_items.order_id references orders.id
- quantity must be greater than zero
- unit price must be greater than zero
- version is configured as an optimistic-concurrency token
- domain objects are mapped to separate persistence entities instead of coupling the Domain model to EF Core

## Running locally

Requirements:

- .NET SDK 10
- Docker Desktop with Linux containers
- repo-local .NET tools restored with dotnet tool restore

Restore tools and build:

~~~powershell
dotnet tool restore
dotnet restore
dotnet build PayFlow.slnx
~~~

### Start Order PostgreSQL

The compose file is deploy/docker-compose.yml.

~~~powershell
$env:ORDER_DB_PASSWORD="payflow-local-dev"
$env:ORDER_DB_PORT="5433"

docker compose -f deploy/docker-compose.yml up -d
docker compose -f deploy/docker-compose.yml ps
~~~

The custom host port is useful when another PostgreSQL instance already occupies port 5432.

Configure the Order API connection string:

~~~powershell
$env:ConnectionStrings__OrderDatabase="Host=localhost;Port=5433;Database=orders_db;Username=payflow_order;Password=$env:ORDER_DB_PASSWORD"
~~~

### Apply EF Core migrations

~~~powershell
dotnet ef database update --project src/Services/Order/PayFlow.Order.Infrastructure/PayFlow.Order.Infrastructure.csproj --startup-project src/Services/Order/PayFlow.Order.Api/PayFlow.Order.Api.csproj --context OrderDbContext
~~~

List migrations:

~~~powershell
dotnet ef migrations list --project src/Services/Order/PayFlow.Order.Infrastructure/PayFlow.Order.Infrastructure.csproj --startup-project src/Services/Order/PayFlow.Order.Api/PayFlow.Order.Api.csproj --context OrderDbContext
~~~

### Run Order API

~~~powershell
dotnet run --project src/Services/Order/PayFlow.Order.Api/PayFlow.Order.Api.csproj --urls http://localhost:5101
~~~

Create an order:

~~~powershell
$body = @{
    customerId = [guid]::NewGuid().ToString()
    items = @(
        @{
            sku = "SKU-001"
            quantity = 2
            unitPrice = 10.00
            currency = "USD"
        },
        @{
            sku = "SKU-002"
            quantity = 3
            unitPrice = 5.00
            currency = "USD"
        }
    )
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri "http://localhost:5101/orders" -ContentType "application/json" -Body $body
~~~

Expected result:

~~~text
status      : Pending
totalAmount : 35
currency    : USD
~~~

## Tests

Run Order unit tests:

~~~powershell
dotnet test tests/Unit/PayFlow.Order.UnitTests/PayFlow.Order.UnitTests.csproj
~~~

Run Order integration tests:

~~~powershell
dotnet test tests/Integration/PayFlow.Order.IntegrationTests/PayFlow.Order.IntegrationTests.csproj
~~~

The integration-test project uses Testcontainers so tests can run against a real ephemeral PostgreSQL instance instead of an in-memory database.

## Reliability principles

The project intentionally emphasizes correctness over the number of endpoints.

Key principles include:

- database per service
- eventual consistency between bounded contexts
- at-least-once message delivery
- Inbox/Outbox patterns
- business-level idempotency
- persisted Saga state
- explicit compensation
- optimistic/pessimistic concurrency where appropriate
- immutable double-entry financial ledger
- retries, backoff, DLQ, and reconciliation
- no end-to-end exactly-once claim

## Documentation

Architecture documents are under docs/architecture/.

Architectural Decision Records are under docs/adr/.

These documents explain not only the selected design, but also alternatives considered and why they were rejected or deferred.

## Roadmap

Near-term implementation:

1. PostgreSQL Testcontainers fixture and Order persistence integration tests
2. API validation and error mapping
3. Order idempotency
4. Transactional Outbox
5. Order state-transition use cases
6. Inventory Service
7. Payment Service and immutable ledger
8. Saga orchestration
9. Kafka integration, Inbox/DLQ/retry policies
10. observability, load tests, CI, Docker/Kubernetes deployment

The project is developed incrementally in small, reviewable commits so architectural and reliability decisions remain visible in the Git history.
