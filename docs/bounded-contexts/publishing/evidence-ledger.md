# Publishing evidence ledger

```yaml
context: publishing
status: piloted
journey: PUBLISHING.MANAGE (Builder creates projects, defines structure once, manages units and clients)
feature: publishing-convergence
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
    result: 206/206 green (15 architecture + 20 contract + 41 integration + 130 modules)
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 9/9 green across journeys; seeded and e2e rows cleaned with orphan sweep
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
mutations:
  - mutant: project ownership checks removed (any builder manages any project)
    killed_by: PublishingAccessTests create/read/update/delete ownership tests
  - mutant: duplicate-unit guard removed (unique violation escapes as 500)
    killed_by: Duplicate_unit_conflicts_and_parallel_define_stays_single
  - mutant: structure one-shot flag removed (redefine provisions twice)
    killed_by: redefine-409 plus parallel-define single-winner tests
  - mutant: structure validation removed (zero floors provision)
    killed_by: STRUCTURE_VALIDATION 422/400/404/403 matrix
skip_reasons: []
commands:
  - command: dotnet test backend/tests/Modules --filter PublishingAccessTests
    result: 3/3 passed (project create/read/update/delete ownership, structure plus unit ownership, client ownership)
  - command: npm run test:unit (frontend)
    result: 36/36 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5 + publishing-contract 4/4, last local run)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter PublishingPersistenceMySqlTests (temporary 3306:3306 mapping, reverted afterwards)
    result: "2/2 passed against live MySQL 8.0 (deterministic structure: 6 units, 6 floor plus 12 unit devices, redefine 409; ownership boundaries hold); probe rows cleaned"
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: publishing-manage.spec.js 1/1 green (UI project create, structure via API, units visible, grid lists it); full suite 9/9; seeded projects/units/clients/devices/users cleaned with orphan sweep
artifacts: []
failures:
  - class: test
    evidence_for: [per-builder cleanup hardcoded to one id left Dupe Towers rows behind across reruns]
    evidence_against: [all other tables cleaned by key]
    verdict: cleanup parameterized by builder id; proven by zero leftovers after consecutive runs
  - class: product
    evidence_for: [project read/update/delete, structure definition, unit creation/assignment, and client create/read/update/delete enforced login but zero ownership]
    evidence_against: [project list already scoped to caller]
    verdict: token-builder ownership on every mutating and item route (403 on explicit mismatch, 404 on foreign ids, creation bound to caller); unfiltered unit and client lists stay visible by design, recorded as scheduled risks
open_risks:
  - risk: unit and client lists have no per-role visibility scoping (frontend depends on unfiltered reads)
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Builder
    happy_path: frontend/tests/e2e/publishing-manage.spec.js
```
