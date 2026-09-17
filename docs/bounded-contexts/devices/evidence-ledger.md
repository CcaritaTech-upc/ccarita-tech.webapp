# Devices evidence ledger

```yaml
context: devices
status: piloted
journey: DEVICES.CONTROL (Owner sends commands to own unit devices)
feature: devices-convergence
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
    result: 197/197 green (15 architecture + 20 contract + 41 integration + 121 modules)
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 8/8 green across journeys; e2e and seeded rows cleaned afterwards
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
mutations:
  - mutant: PUT/DELETE ownership checks removed (any user edits any device)
    killed_by: OWNER/BUILDER manage-own plus foreign-404 and anonymous-401 tests
  - mutant: telemetry field guard removed (null payload reaches EF)
    killed_by: minimal-payload partition expecting 400 (proven red 500 without the guard during development)
  - mutant: per-device lock removed (burst corrupts shadow)
    killed_by: CONCURRENT_SAME_DEVICE 8-way burst asserting one coherent shadow
  - mutant: attribute validation removed (garbage actuates)
    killed_by: CONTROL_REJECTS and Tier D fuzz partitions
skip_reasons: []
commands:
  - command: dotnet test backend/tests/Modules --filter DeviceManageTests
    result: 3/3 passed (owner/builder manage own devices, foreign 404, anonymous 401)
  - command: npm run test:unit (frontend)
    result: 32/32 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5, last local run)
  - command: dotnet test backend/tests/Modules --filter DeviceControlFlowTests
    result: 2/2 passed (command→telemetry→status convergence; wrong role/unit/attribute/range/missing/anonymous rejections)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter DeviceControlMySqlTests (temporary 3306:3306 mapping, reverted afterwards)
    result: 3/3 passed against live MySQL 8.0 (command plus shadow durability, telemetry durability visible on status, per-unit-type duplicate 409 with MAC intentionally ignored on the owner-custom path); probe rows cleaned
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: devices-control.spec.js 1/1 green (builder provisions, owner registers with auto-link, brightness from UI, desired state on status endpoint); full suite 8/8; seeded projects/units/clients/devices/users cleaned afterwards
artifacts: []
failures:
  - class: product
    evidence_for: [telemetry ingest without eventId/status/payload threw unhandled DbUpdateException to a 500]
    evidence_against: [ingest is anonymous by design and takes raw device payloads]
    verdict: fail-closed field guard in the endpoint (400); covered by the minimal-payload fuzz partition
  - class: test
    evidence_for: [telemetry recovery rows survived test cleanup and poisoned the next run with duplicate-key failures]
    evidence_against: [all other tables were cleaned]
    verdict: cleanup extended to recovery rows by event key; proven by 3 consecutive green runs
  - class: product
    evidence_for: [PUT and DELETE device endpoints required login but enforced zero ownership: any user could edit or delete any device]
    evidence_against: [create-custom path already checks Owner role plus unit ownership]
    verdict: unit-owner-or-project-builder management check on both endpoints (foreign reads as not found); list stays unfiltered by design, recorded as scheduled risk
open_risks:
  - risk: device list has no per-role visibility scoping (frontend fetches all and filters client-side)
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Owner
    happy_path: frontend/tests/e2e/devices-control.spec.js
```
