# Verification layers and convergence points

Verification layers divide responsibility by the kind of evidence they can produce. They are not required to match repository folders or test runners.

## Layer responsibilities

| Layer | Proves | Does not need |
|-------|--------|---------------|
| **Domain** | Pure invariants, value rules, state transitions | UI, HTTP, database |
| **Application** | Use-case orchestration, policies, ports, failure decisions | Real network or browser |
| **Component** | Rendering, interaction, accessibility semantics, emitted events | Complete application |
| **Frontend integration** | Component, store, router, client, and mocked boundary collaboration | Real backend |
| **API** | Routing, binding, authentication, authorization, status codes, error mapping | Browser |
| **Contract** | Request, response, event, and external protocol compatibility | Complete journey |
| **Persistence** | Mappings, schema, constraints, transactions, concurrency, migrations, durability | Browser |
| **System/E2E** | User-visible outcome across the deployed system | Exhaustive internal branching |

Security, resilience, accessibility, performance, and observability are **cross-cutting quality dimensions**. They influence scenarios at multiple layers rather than existing only in one test folder.

## Progressive convergence

### Internal frontend convergence

```text
validation + component + store + router + API client
```

This verifies frontend collaboration while controlling the network boundary.

### Backend and database convergence

```text
API/application + repository adapter + production database engine
```

This proves behavior that fakes and in-memory providers cannot guarantee.

### Frontend and API convergence

```text
client request + API route + payload/schema/error contract
```

Executable contracts should detect disagreement before a complete browser journey is required.

### Global convergence

```text
actor → browser → frontend → API → application → database/integration → observable outcome
```

The E2E test confirms system wiring and the business outcome. It should not repeat every domain boundary value or database constraint.

## Database policy

Use a double when testing orchestration. Use the production database engine when claiming guarantees about:

- unique constraints and foreign keys;
- transaction rollback and atomicity;
- isolation and concurrent requests;
- collation and query behavior;
- migrations and schema compatibility;
- restart durability.

The database instance must be disposable, isolated, migrated through the production path, and version-compatible with production.

## Boundary-first failure localization

When a global test fails, prior convergence evidence narrows the cause:

| Passing evidence | Likely remaining area |
|------------------|-----------------------|
| Backend + database | Frontend behavior or frontend/API mismatch |
| Frontend + API contract | Backend orchestration, persistence, or environment |
| All pairwise boundaries | Deployment wiring, timing, state leakage, or cross-system behavior |

This is why Convergent Testing produces better diagnostics than relying primarily on E2E.
