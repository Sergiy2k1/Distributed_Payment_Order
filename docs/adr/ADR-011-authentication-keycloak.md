# ADR-011: Authentication and Authorization with Keycloak

## Status

Accepted for V1.

## Context

PayFlow exposes public APIs through a Gateway and contains several internal services with independent ownership boundaries.

The system needs to distinguish at least:

- authenticated customers;
- administrators/operators;
- internal service identities.

Authentication and authorization must work without turning PayFlow into an identity-management project.

Building custom username/password storage, token issuance, refresh-token rotation, MFA, account recovery, key rotation, and identity administration would add substantial security-sensitive scope that is not central to the distributed payment/order problem.

## Decision

PayFlow uses **Keycloak** as the external Identity Provider for V1.

The platform uses standards-based:

- OpenID Connect for authentication;
- OAuth 2.0 access tokens for API authorization;
- JWT bearer tokens for service API validation where appropriate.

PayFlow services do not implement their own password database or token issuer.

## Trust boundary

The Gateway validates incoming access tokens and applies edge-level policies.

However, authorization does **not** exist only at the Gateway.

Each owning service must still enforce authorization for operations it exposes.

The rule is:

```text
Gateway = first authorization boundary
Owning service = authoritative authorization boundary for its resource
```

A service must not trust a request merely because it came from the internal network or through the Gateway.

## Initial roles

The baseline user roles are:

### Customer

A Customer may operate on resources they are allowed to own or access.

Typical examples:

- create an Order for themselves;
- read their own Order;
- request allowed customer-facing actions on their own resource.

Customer authorization must include ownership checks where relevant.

A `Customer` role alone is not sufficient to read another customer's Order.

### Admin

An Admin may perform privileged operational actions explicitly exposed for administration.

Potential examples include:

- inventory administration;
- operational inspection;
- selected recovery/management endpoints;
- system-support workflows.

Admin access must remain policy-based rather than implemented as scattered string comparisons.

## Customer identity

The authenticated user identity is derived from stable claims issued by Keycloak.

The exact subject claim mapping will be defined during implementation, but the preferred stable user identifier is the OIDC subject (`sub`) or an explicitly mapped immutable user identifier.

Email, display name, or mutable username must not be used as the durable ownership key.

## Order ownership

Order stores the customer identity required for authorization.

For example:

```text
Order.CustomerId = authenticated stable subject identifier
```

When a customer requests an Order:

1. token identity is validated;
2. Order is loaded by the owning service;
3. the service compares the authenticated subject with the Order owner;
4. access is denied if ownership does not match unless another explicit policy allows it.

This check belongs in the Order service, not only in the Gateway.

## Gateway responsibility

The Gateway may perform:

- token validation;
- public route protection;
- coarse role/policy checks;
- correlation/trace propagation;
- rate limiting when introduced.

The Gateway must not become the only place where resource-level authorization exists.

It also must not contain Order/Inventory/Payment business rules.

## Service authorization

Each API service validates the authentication context it requires.

Depending on deployment topology, services may validate the original JWT or receive an explicitly designed trusted service identity/token flow.

The baseline direction is for APIs to validate bearer tokens using the configured issuer/audience rather than trusting arbitrary forwarded headers.

Headers such as:

```text
X-User-Id
X-Role
```

must not become trusted identity merely because the Gateway supplied them unless a cryptographically trusted internal mechanism is explicitly designed.

## Service-to-service identity

Synchronous internal service calls, if introduced, use machine identity rather than impersonating a random customer by default.

The baseline direction is OAuth 2.0 Client Credentials for service identities where appropriate.

Examples of service identities may include:

- Gateway;
- administrative worker;
- future internal RPC client.

Kafka-based core checkout messages are authorized operationally through Kafka ACLs/service credentials and contract ownership rather than by embedding end-user bearer tokens in every event.

## End-user identity in asynchronous workflows

Integration messages may carry the minimum business identity required by the receiving bounded context.

For example, `OrderCreated.v1` may carry `CustomerId` if Saga or a downstream workflow genuinely requires it.

However:

- access tokens are not placed in Kafka messages;
- refresh tokens are never placed in Kafka;
- authentication cookies are never placed in Kafka;
- services must not serialize the full JWT into business events.

Tokens expire; business events may live much longer.

Asynchronous workflows use durable business identifiers, not transient authentication credentials.

## Authorization policy model

Authorization should be expressed through named policies/requirements rather than scattered imperative checks.

Examples may include:

```text
CustomerAccess
AdminOnly
OwnOrder
InventoryAdministration
OperationalRecovery
```

The exact policy names may evolve.

Resource-based authorization is required where access depends on the loaded business resource.

## Keycloak configuration ownership

Keycloak realm/client configuration is infrastructure configuration, not domain state.

The repository may later contain reproducible development configuration/export for:

- realm;
- clients;
- roles;
- service accounts;
- test users for local development.

Production secrets must not be committed.

## Secrets

The repository must not contain:

- real client secrets;
- passwords;
- signing private keys;
- production tokens.

Local development secrets use environment variables, secret stores, or development-only configuration mechanisms.

Kubernetes/deployment secrets are handled separately during deployment work.

## Token validation

Services must validate at minimum the properties appropriate to the token type:

- issuer;
- signature;
- expiration;
- audience where configured;
- token type/claims required by policy.

Authorization must fail closed when required identity data is absent or invalid.

Clock-skew tolerance must be deliberate and bounded.

## Alternatives considered

### Custom ASP.NET Core Identity as full authentication system

This is technically possible and remains a viable architecture in other projects.

Rejected for PayFlow V1 because the project would then need to own:

- credential storage;
- password security;
- token issuance;
- refresh token lifecycle;
- account recovery;
- email verification;
- MFA decisions;
- identity administration;
- signing-key lifecycle.

That scope distracts from the distributed payment/order reliability goals.

### Custom JWT generation without an identity platform

Rejected.

A minimal custom issuer may look simple initially but quickly creates security-sensitive requirements around:

- signing keys;
- rotation;
- revocation;
- refresh tokens;
- claim design;
- user lifecycle.

Using a standards-compliant IdP keeps this responsibility outside PayFlow.

### Trust the Gateway only

Rejected because bypass, misconfiguration, future internal routes, or deployment changes could expose services without resource-level protection.

Defense in depth requires owning services to enforce authorization too.

### Put user JWT in Kafka messages

Rejected because:

- JWTs expire;
- messages may be replayed later;
- tokens contain more claims than most consumers need;
- sensitive credentials would spread through logs/topics;
- authorization at processing time should rely on durable business context and service trust, not a stale user token.

### One shared internal static API key

Rejected as the long-term service-identity model because it provides weak identity granularity and rotation/audit semantics.

## Consequences

Positive consequences:

- PayFlow avoids implementing security-sensitive identity infrastructure;
- OIDC/OAuth2 are demonstrated in a realistic architecture;
- user and service identities are separated;
- authorization remains aligned with bounded-context ownership;
- resource-level ownership checks are not hidden in the Gateway;
- future SSO/MFA features remain possible through the IdP.

Negative consequences:

- local development requires Keycloak;
- another infrastructure component must be configured and monitored;
- realm/client configuration needs reproducibility;
- role/claim mapping must remain synchronized with application policies;
- integration tests need token issuance/test identity setup.

These costs are acceptable because identity is delegated to a component designed for it.

## Testing requirements

At minimum, tests must prove:

- unauthenticated public protected endpoint is rejected;
- expired/invalid token is rejected;
- Customer can access their own Order;
- Customer cannot access another customer's Order;
- Admin policy works only for Admin role;
- Gateway denial works;
- direct service request still enforces authorization;
- missing required subject claim fails closed;
- service credential cannot accidentally gain Customer resource access unless explicitly allowed;
- Kafka messages do not contain bearer/refresh tokens.

## Implementation constraints

When authentication implementation begins:

1. Keycloak is the V1 Identity Provider;
2. PayFlow does not store user passwords;
3. stable subject identity is used for resource ownership;
4. Gateway validates public authentication;
5. owning services enforce their own authorization;
6. Customer ownership checks are resource-based;
7. Admin actions use explicit policies;
8. service-to-service identity is distinct from end-user identity;
9. access/refresh tokens are not embedded in Kafka events;
10. secrets are never committed to the repository.

## Review trigger

Revisit this ADR if:

- deployment environment already provides another standards-compliant enterprise IdP;
- Keycloak operational overhead becomes unjustified;
- application requirements require local user credential ownership;
- external identity federation requirements materially change.

A replacement should continue to use standards-based authentication and preserve service-level authorization boundaries.
