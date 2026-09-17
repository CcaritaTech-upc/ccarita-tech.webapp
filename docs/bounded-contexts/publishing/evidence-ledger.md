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
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
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
