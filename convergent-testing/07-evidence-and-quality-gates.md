# Evidence and quality gates

Convergent Testing treats test output as evidence tied to a journey and scenario. A green summary without traceability is insufficient for diagnosing system-level failures.

## Evidence by layer

| Layer | Minimum evidence |
|-------|------------------|
| Domain/application | Test identifier, assertion failure, deterministic inputs |
| Component/frontend integration | Rendered state, user action, accessibility-facing result |
| Contract/API | Method, route, status, sanitized request/response schema |
| Persistence | Database version, migration version, transaction result, relevant constraint/error code |
| System/E2E | Playwright trace on failure, screenshot when useful, console, failed requests, scenario identifier |
| Resilience/performance | Fault injected, duration, retry behavior, resource/latency result |

Evidence must be sanitized, reproducible, retained for an agreed duration, and connected through stable journey/scenario identifiers.

## Quality gates

### Gate 0 — Local correctness

- Domain and application rules pass.
- Components and frontend integrations pass.
- No known deterministic failure is hidden by retries.

### Gate 1 — Boundary convergence

- API contracts are executable and compatible.
- Backend behavior is proven with its persistence adapter where required.
- Production-specific database guarantees run against the production engine.

### Gate 2 — Happy-path system convergence

- The critical journey passes through the complete deployed path.
- Test data is isolated and repeatable.
- Required diagnostic artifacts are produced on failure.

### Gate 3 — Risk portfolio

- Required Tier A scenarios pass.
- Tier B/C/D coverage follows project cadence.
- Security and irreversible data risks cannot be downgraded by rarity alone.

### Gate 4 — Release evidence

- No unresolved critical flaky test exists.
- Environment and migration versions are known.
- Relevant journey regressions are linked to the change.

## Flakiness policy

Retries provide evidence; they do not convert a flaky test into a passing test.

When a retry passes:

1. mark the execution as unstable;
2. retain the first failure trace;
3. classify the source of nondeterminism;
4. fix or quarantine with an owner and expiration;
5. never silently ignore the scenario.

## Metrics

Use several signals instead of a single coverage percentage:

- critical journeys with a passing happy path;
- required scenarios covered by risk tier;
- convergence points with executable evidence;
- escaped defects mapped back to missing scenarios;
- flaky-test rate and age;
- mean diagnostic time;
- mutation score for selected critical logic;
- line/branch coverage as a supporting signal.

The primary metric is confidence in user outcomes, not the number of test files.

## Journey completion checklist

- [ ] Acceptance criteria are observable and unambiguous.
- [ ] Business rules have authoritative owners.
- [ ] Scenario risks are classified.
- [ ] Assertions are assigned to the cheapest trustworthy layers.
- [ ] Required pairwise boundaries converge.
- [ ] The global E2E proves the outcome without duplicating exhaustive lower-level cases.
- [ ] Failure evidence supports agent and human diagnosis.
- [ ] Final acceptance runs without MCP or hidden interactive state.
