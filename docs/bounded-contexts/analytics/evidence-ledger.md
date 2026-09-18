# Analytics evidence ledger

```yaml
context: analytics
status: piloted
journey: ANALYTICS.VIEW (Builder dashboard and Owner dashboard variants)
feature: analytics-convergence
gates:
  G0: passed
  G1: passed
  G2: passed
  G3: passed
  G4: passed
environment:
  database: mysql:8.0 (production engine for all persistence proofs)
  migrations: 202608280001_FoundationSchema → 202608290002_IamAndDispatch → 202608290003_CoreBusiness → 202608300004_DevicesTelemetry → 202608300005_AnalyticsProjections → 202609170006_SubscriptionActiveArbiter
  backend: .NET 9 (CI setup-dotnet 9.0.x)
  frontend: node 22 (CI setup-node 22)
reruns:
  - command: dotnet test backend/IoBuild.sln with live MySQL (temporary 3306:3306 mapping, reverted afterwards)
    result: 216/216 green (15 architecture + 20 contract + 41 integration + 140 modules)
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 11/11 green across journeys; e2e and seeded rows cleaned afterwards
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
mutations:
  - mutant: ownership checks removed (any user reads any dashboard)
    killed_by: AnalyticsAccessTests self-match and scoping tests
  - mutant: projection self-sync removed (dashboards go empty)
    killed_by: dashboard data tests asserting seeded metrics
  - mutant: per-user sync gate removed (parallel loads double-insert)
    killed_by: Concurrent_dashboards 6-way stress (proven red with duplicate-key 500s without the gate during development)
  - mutant: energy clamp removed (absurd windows pass through)
    killed_by: ENERGY_WINDOW clamp test
skip_reasons: []
commands:
  - command: dotnet test backend/tests/Modules --filter AnalyticsAccessTests
    result: 2/2 passed (metrics/energy self-match with 403 cross and 401 anonymous; insights scoped to owned projects)
  - command: npm run test:unit (frontend)
    result: 39/39 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5 + publishing-contract 4/4 + analytics-contract 3/3, last local run)
  - command: dotnet test backend/tests/Modules --filter AnalyticsDashboardTests
    result: 3/3 passed (builder and owner dashboard data, energy window clamp)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter Dashboards_and_projections_are_durable_on_mysql (temporary 3306:3306 mapping, reverted afterwards)
    result: 1/1 passed against live MySQL 8.0 (both dashboards with data plus project/unit/device projection durability); probe rows cleaned
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: analytics-view.spec.js 2/2 green (Builder dashboard with own project metrics, Owner dashboard view); full suite 11/11; e2e and seeded rows cleaned afterwards
artifacts: []
failures:
  - class: product
    evidence_for: [parallel dashboard loads double-inserted projections and 500d the losers on duplicate primary keys]
    evidence_against: [projection self-sync assumes a quiet tenant]
    verdict: per-user-id async gate around both dashboard queries (same in-process idiom as device locks; multi-instance would need a database arbiter); stress-proven 3/3 with single projection rows
  - class: product
    evidence_for: [all 5 analytics routes answered anonymously for any user id, including PII-adjacent consumption metrics]
    evidence_against: [every frontend caller passes its own user id]
    verdict: RequireAuthorization plus token-id self-match on metric/energy routes (403 on mismatch); insights scoped to builder-owned or unit-occupied projects (404 otherwise)
open_risks: []
roles_covered:
  - role: Builder
    happy_path: frontend/tests/e2e/analytics-view.spec.js (Builder test)
  - role: Owner
    happy_path: frontend/tests/e2e/analytics-view.spec.js (Owner test)
```
