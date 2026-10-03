# Publishing evidence ledger

## Backend test navigation — 2026-10-02

The [Publishing catalogue](test-catalog.md) maps management, structure, units
and clients to the unchanged scenarios. [Reorganization evidence](../../../backend/tests/reorganization-evidence.md)
records local source/discovery preservation and solution runs; it does not claim
new relational atomicity, runtime-stack or CI acceptance.

See the [crosscutting frontend performance report](../../performance/evidence-ledger.md)
for shared gzip/cache/chunks, functional E2E evidence and the new local
mobile/desktop Lighthouse audit of Builder project list/create/detail and client
list/detail. Exact routes and fixture scope are in the central report. Local
observations do not establish CI acceptance or all-state performance coverage.

The central round-two matrix remeasures these routes after shared header/theme
changes and runs the complete publishing journey twice. No Publishing business
behavior or context-specific CPU optimization changed.

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
  - risk: unfiltered unit listing has no per-role visibility scoping
    owner: ccarita-tech
    review_by: 2026-10-06
roles_covered:
  - role: Builder
    happy_path: frontend/tests/e2e/publishing-manage.spec.js
client_tenant_read_and_assignment_isolation:
  journey: Each authenticated Builder lists and manages only clients associated with its owned projects and units.
  actor_coverage:
    - actor: Builder A
      outcome: Sees only own clients; cannot query Builder B by id or move a client/unit association across projects.
    - actor: Builder B
      outcome: Retains access to its own client list and assignments.
    - actor: Owner
      outcome: Cannot use Builder client-list endpoints.
  scenarios:
    - tier: A
      scenario: List filters by token BuilderId; spoofed builder/project filters are denied.
      owner: backend/tests/Modules/Publishing/Management/Api/PublishingAccessTests.cs
    - tier: A
      scenario: Create/update reject foreign project or unit references and cannot reassign BuilderId.
      owner: backend/tests/Modules/Publishing/Management/Api/PublishingAccessTests.cs
    - tier: A
      scenario: Invitation lookup does not reveal client or unit PII.
      owner: backend/tests/Modules/IAM/AccountLifecycle/Api/IamApiContractTests.cs
    - tier: A
      scenario: Admin-only user directory denies Builder tokens.
      owner: backend/tests/Modules/IAM/AccountLifecycle/Api/IamApiContractTests.cs
  gates:
    G0: passed
    G1: passed
    G2: passed
    G3: passed
    G4: skipped — deterministic local repeat passed; CI not run from this workspace.
  commands:
    - command: dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter FullyQualifiedName~PublishingAccessTests --verbosity minimal
      result: 3/3 passed after adding Builder-role guards to client item reads and delete.
    - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
      result: 236/236 passed (17 architecture + 20 contract + 42 integration + 157 modules); opt-in MySQL tests skip when not configured.
    - command: E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npx playwright test --workers=2
      result: 29/29 passed against the isolated full stack with MySQL 8; tenant-list separation, foreign project/unit attempts, Owner denial, and invitation PII minimization.
    - command: E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npx playwright test tests/e2e/tenant-security.spec.js --workers=1 --repeat-each=2
      result: 2/2 deterministic repeat against isolated MySQL 8.
  convergence_points:
    - Builder A list response excludes Builder B client PII.
    - Cross-builder/project/unit reads and writes are rejected by the API.
    - Public invitation response contains no client or unit PII.
  open_risks:
    - risk: Unit collection endpoints still allow broad authenticated reads and need a separate role/ownership contract.
      owner: ccarita-tech
      review_by: 2026-10-06
 ```

## Confirmed Builder client UI localization

```yaml
context: publishing
feature: builder-client-list-and-profile-localization
journey: Builder reviews assigned clients, opens add/edit forms, and reads unit-device status in the selected language.
actor_coverage:
  - actor: Builder
    outcome: Client fields, add/edit dialogs, paginator report, unit labels, and linked-device counts remain localized across English and Spanish.
scenarios:
  - tier: B
    scenario: PrimeVue paginator placeholders render as localized values instead of untranslated tokens.
    owner: frontend/tests/e2e/builder-ui-localization.spec.js
  - tier: B
    scenario: Client list, add/edit dialogs, unit/profile copy, and device-count copy update after language switching.
    owner: frontend/tests/e2e/builder-ui-localization.spec.js
layer_ownership:
  G0: Frontend unit suite and production build validate the localized copy helper and assets.
  G1: Client API contract is unchanged; the system journey uses the existing MySQL-backed client endpoints.
  G2: Playwright asserts client visibility, paginator report, both dialogs, and unit-device count for a Builder.
  G3: Tenant isolation and role guards remain covered by `tenant-security.spec.js` and backend access tests.
  G4: The full clean-stack E2E suite passed with one and two workers.
gates:
  G0: passed
  G1: passed
  G2: skipped — local Playwright evidence is green; CI has not run on this worktree.
  G3: skipped — local risk campaigns passed; CI acceptance is pending.
  G4: skipped — clean local reruns passed; CI delivery evidence is pending.
commands:
  - command: npm run test:unit
    result: 76/76 passed.
  - command: npm run build
    result: passed.
  - command: E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npm run test:e2e -- --workers=2
    result: 30/30 passed against an isolated MySQL 8 stack on the final clean-stack rerun.
artifacts:
  - Playwright traces/screenshots/videos remain under the authorized temporary E2E output folder on failure.
open_risks: []
```
