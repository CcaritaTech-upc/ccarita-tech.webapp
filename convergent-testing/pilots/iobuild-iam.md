# IoBuild IAM pilot

The first Convergent Testing pilot will protect account registration, authentication, authorized access, logout, and durable token revocation across the Vue frontend, ASP.NET Core backend, and MySQL persistence.

## Pilot outcome

Prove this System Journey:

```text
Visitor registers
    ↓
Account and related durable state are committed
    ↓
User signs in
    ↓
Token authorizes a protected request
    ↓
User signs out
    ↓
The same token remains rejected after backend restart
```

Stable journey identifiers:

```text
IAM.REGISTRATION
IAM.LOGIN
IAM.AUTHORIZED_ACCESS
IAM.LOGOUT
```

These may be reported separately while participating in one pilot journey.

## Current evidence

IoBuild already contains useful tests, but the existing project names do not consistently represent test layers:

- `backend/tests/Architecture` checks selected architecture and repository conformance.
- `backend/tests/Contract` primarily inspects configuration and static catalogs rather than executing HTTP contracts.
- `backend/tests/Integration` primarily invokes services using EF Core InMemory.
- `backend/tests/Modules` mixes application, persistence, external-adapter, API, security, and optional MySQL tests.
- `IamWorkflowTests.cs` includes a registration → login → logout → rejected-token API happy path.

This pilot will classify existing evidence before adding new tests. Moving files is not required during the first iteration.

## Happy-path convergence map

| Layer | Pilot proof |
|-------|-------------|
| Domain/application | Email normalization, password hashing decision, registration orchestration, sign-in decision, revocation decision |
| Frontend component | Registration and login forms expose valid states, feedback, and accessible controls |
| Frontend integration | Forms collaborate with API clients, session state, and router |
| API | Registration, session creation, protected access, logout, and revoked-token rejection use the expected routes and responses |
| Contract | Frontend payloads and response/error schemas match the backend |
| Persistence | User, related links, dispatch state, and revocation are committed atomically and survive new contexts/restarts |
| System/E2E | A real browser completes the user-visible journey against the deployed backend and MySQL |

## Database pilot rule

Fast application tests may use controlled doubles. Claims about transactions, uniqueness, concurrency, migrations, or durability must use a disposable MySQL instance compatible with production.

The pilot must:

- align the test MySQL version with deployment;
- apply the production migration path rather than substituting `EnsureCreated`;
- isolate data per execution or worker;
- prove rollback when registration fails after an intermediate write;
- prove one account under concurrent duplicate registration;
- prove revocation after a fresh backend/database context;
- avoid shared or production data.

## Initial scenario portfolio

### Happy path

- Valid registration.
- Valid login.
- Protected request succeeds.
- Logout succeeds.
- Revoked token is rejected after restart.

### Tier A

- Required fields are missing.
- Email or password violates documented policy.
- Cross-field confirmation does not match.
- A field that must differ from another contains the same value.
- Client-provided role attempts privilege escalation.
- Duplicate registration is submitted sequentially and concurrently.
- Incorrect password does not create a token.
- A registration failure rolls back all durable state.
- Repeated submission does not create duplicate side effects.

### Tier B

- API returns a safe error during database or dependency timeout.
- Token expires during an active browser session.
- User navigates away while authentication is pending.
- Dispatch processing retries and recovers after an expired lease.
- Logout is repeated or interrupted.

### Tier C

- Email normalization interacts with Unicode and MySQL collation.
- Multiple tabs hold conflicting session state.
- Lock contention occurs during concurrent registration.
- Existing representative IAM data survives schema migration.

### Tier D

- Malformed or oversized payloads bypass the frontend.
- Corrupt dependency responses and unusual timing sequences are injected.
- Focused fuzzing, mutation testing, and resource-pressure experiments challenge critical rules.

## Form-rule policy

Registration and login forms will classify rules as:

- field-level;
- cross-field;
- contextual/business;
- authorization;
- temporal;
- persistence-backed.

Frontend tests prove feedback and interaction. Backend tests prove authoritative enforcement. Database tests prove final integrity where concurrency or relations are involved.

## Diagnostic pilot

When the coded E2E fails:

1. retain Playwright trace, failed requests, console output, and useful screenshots;
2. correlate requests with backend logs and sanitized database evidence;
3. classify product, test, environment, flaky, specification-gap, or unknown failure;
4. use Playwright MCP only for controlled interactive investigation;
5. encode the correction in application or test code;
6. rerun the Playwright test without MCP from a clean context;
7. run affected lower and adjacent layers;
8. record any framework gap discovered during diagnosis.

## Lessons captured during the pilot

- `WebApplicationFactory` must remove hosted services and disable MQTT; otherwise API tests depend on a live broker.
- Executable nginx expectations must match the deployed topology (proxy to frontend vs static `try_files`).
- Stripe integrations fail closed on `rk_` only; `sk_` keys never enable transport.
- InMemory ignores column limits, so the registration workflow enforces email presence and the 320-character bound authoritatively and the API maps violations to `400`.
- Tier D oversized-payload probing discovered the missing guard above; the regression now lives in `IamTierDTests`.
- Register performs sign-up plus sign-in plus navigation to home; E2E resets to an anonymous session before proving login.
- Proof scripts and deployment both pin `mysql:8.0`; CI guards the engine version.

## Pilot execution phases

### Phase 1 — Inventory

- Map existing IAM tests to journey, scenario, layer, risk, and dependency.
- Record missing evidence without rewriting the suite.

### Phase 2 — Happy-path convergence

- Establish frontend test infrastructure.
- Create executable frontend/API contract evidence.
- Establish disposable production-compatible MySQL testing.
- Create the deterministic Playwright happy path.

### Phase 3 — Tier A

- Add critical form, API, authorization, transaction, concurrency, and idempotency scenarios.

### Phase 4 — Diagnostic exercise

- Introduce or select a real failure.
- Run the agent-assisted diagnostic loop.
- measure diagnostic quality and maintenance cost;
- update framework documentation from observed gaps.

### Phase 5 — Broader risk tiers

- Add Tier B and selected Tier C scenarios.
- Schedule Tier D campaigns only after lower-tier stability.

## Pilot exit criteria

- [ ] Existing IAM evidence is classified by stable journey identifiers.
- [ ] Every assertion has a clear primary layer.
- [ ] Frontend/API and backend/database boundaries have executable evidence.
- [ ] The happy path passes as a deterministic Playwright test without MCP.
- [ ] Tier A security and integrity scenarios pass.
- [ ] At least one real failure completes the agent diagnostic loop.
- [ ] Lessons update the framework before agent-skill design begins.
