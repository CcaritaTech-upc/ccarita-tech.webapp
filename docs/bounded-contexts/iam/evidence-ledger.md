# IAM evidence ledger

```yaml
context: iam
status: piloted
journey: IAM.REGISTRATION → IAM.LOGIN → IAM.AUTHORIZED_ACCESS → IAM.LOGOUT
feature: iobuild-iam-convergence
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
  frontend: node 22 (CI setup-node 22, engines ^20.19.0 || >=22.12.0)
reruns:
  - command: dotnet test backend/IoBuild.sln with live MySQL (temporary 3306:3306 mapping, reverted afterwards)
    result: 220/220 green (15 architecture + 20 contract + 41 integration + 144 modules, including Tier D burst/auth/fat-payload/dead-database campaigns)
  - command: npm run test:unit
    result: 14/14 green on consecutive runs
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 2/2 green on consecutive runs; e2e.* evidence rows cleaned afterwards, outbox left at zero
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
  history:
    - case: concurrent duplicate test passed once by timing luck, then failed 6/6 under stress
      verdict: genuine race (1062), fixed deterministically in product; never quarantined or retried-green
mutations:
  - mutant: PasswordHasher.Verify returns true unconditionally
    killed_by: IAM_LOGIN_HASH_MUTANT
  - mutant: registration email guard removed
    killed_by: IAM_REGISTRATION_GUARD_MUTANTS
  - mutant: registration password guard removed
    killed_by: IAM_REGISTRATION_GUARD_MUTANTS
  - mutant: role whitelist removed (any role accepted)
    killed_by: IAM_REGISTRATION_UNKNOWN_ROLE + IAM_REGISTRATION_GUARD_MUTANTS
  - mutant: role canonicalization removed
    killed_by: IAM_REGISTRATION_ROLE_IS_CANONICAL
  - mutant: duplicate-1062 catch removed (race throws 500)
    killed_by: Concurrent_duplicate_registration (proven red 6/6 without the catch during development)
  - mutant: revocation write removed (logout never persists)
    killed_by: Revocation_on_mysql_is_durable_across_contexts + Owner E2E revoked-401 assertion
skip_reasons: []
commands:
  - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
    result: 139/139 passed without MySQL (opt-in tests skip-with-success; last full local run)
  - command: IOBUILD_TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=3306;Database=iobuild;User=root;Password=iobuild" dotnet test backend/IoBuild.sln --verbosity minimal (with a temporary 3306:3306 host mapping on mysql-monolith, reverted afterwards)
    result: 146/146 passed against live MySQL 8.0 (15 architecture + 20 contract + 41 integration + 70 modules, including Tier A escalation/canonical, concurrent duplicate x6 stress, and post-write rollback)
  - command: same full-solution run after removing stale pre-existing outbox rows (arroz/wasa/dbproof, owner-approved) with a quiet table
    result: 146/146 passed with every MySQL opt-in test fully executing, outbox table left at zero rows
  - command: same full-solution run with MySQL after adding Tier B safe-error contract, 8-way burst contention, and migration-survival proofs (temporary 3306:3306 mapping, reverted afterwards)
    result: 149/149 passed (15 architecture + 20 contract + 41 integration + 73 modules); scratch migration database dropped, outbox table left at zero rows
  - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal (no MySQL; opt-in tests skip-with-success)
    result: 153/153 passed (15 architecture + 20 contract + 41 integration + 77 modules, including 4 new Tier D fuzz/mutation-target campaigns run 3x with no flakiness)
  - command: npm run test:unit (frontend)
    result: 14/14 Vitest passed (validators 7/7 + iam-contract 7/7, last local run)
  - command: npx playwright test (frontend, E2E_BASE_URL=http://localhost:8081 against deployed nginx + dist + API + MySQL)
    result: 2/2 passed — iam-happy-path.spec.js (Owner register/login/logout/revoked-401) and iam-builder.spec.js (Builder register/login), last local run
  - command: npx playwright test (frontend, default vite dev 5173 proxied to stack API)
    result: 2/2 passed, last local run
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 4/4 passed — both happy paths plus iam-form-feedback.spec.js (field-specific registration errors; generic login failure with no oracle); e2e/feedback evidence rows cleaned afterwards
  - command: dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --verbosity minimal
    result: 66/66 passed, including 3 new MySQL opt-in persistence tests (skip-with-success without IOBUILD_TEST_MYSQL_CONNECTION)
  - command: IOBUILD_TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=3306;Database=iobuild;User=root;Password=iobuild" dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --filter "FullyQualifiedName~IamPersistenceMySqlTests" (with a temporary 3306:3306 host mapping on mysql-monolith, reverted afterwards)
    result: 3/3 passed against live MySQL 8.0 (duplicate idempotency, fail-closed rollback, revocation durability across contexts)
  - command: npx playwright test (frontend/tests/e2e/iam-happy-path.spec.js)
    result: 1/1 passed against real stack, Owner only (last local run)
  - command: node scripts/verify-agent-compatibility.mjs
    result: Agent compatibility verified for 4 skills.
  - command: CI run 35170607595 on main (build-and-test, frontend-unit, frontend-e2e, agent-compatibility)
    result: success on all jobs — first green E2E run in CI against the compose stack
artifacts:
  - Playwright trace/screenshot/video on failure (configured: trace on-first-retry, screenshot only-on-failure, video retain-on-failure)
failures:
  - class: product
    evidence_for: [Tier D oversized payload accepted by InMemory-backed workflow]
    evidence_against: [unit tests green, contract tests green]
    verdict: missing authoritative input guard; fixed with fail-closed email/password guard mapped to HTTP 400
  - class: product
    evidence_for: [Tier A registration with role Admin returned 201 and minted an Admin JWT claim accepted by cutover admin gates]
    evidence_against: [business rule requires unknown roles rejected; endpoint tests green for Builder/Owner]
    verdict: missing role whitelist; fixed with fail-closed Builder/Owner whitelist plus canonical casing, mapped to HTTP 400
  - class: product
    evidence_for: [Tier A concurrent duplicate registration threw DbUpdateException 1062, surfacing HTTP 500 instead of idempotent success]
    evidence_against: [sequential duplicate tests green; business rule requires idempotent duplicates]
    verdict: check-then-insert race on the unique email index; fixed by converting error 1062 into idempotent return after detaching the failed insert
  - class: test
    evidence_for: [legacy MySQL lease test leased foreign rows and self-polluted on failure against the shared dev outbox table]
    evidence_against: [InMemory lease mechanics green]
    verdict: test assumed a quiet table; fixed with scoped cleanup plus a quiet-table guard; stale pre-existing rows removed with owner approval and the full proof re-ran green, leaving the table at zero rows
open_risks:
  - risk: Tier B/C browser scenarios declared, not automated (conflicting tab sessions, token expiry mid-session, navigate-away during auth) — flaky-prone, revisit on cadence
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: Tier D resource-pressure experiments and corrupt-dependency injection now covered (50-burst, malformed auth, megabyte payload, dead-database fail-fast)
    owner: ccarita-tech
    review_by: 2026-10-01
    disposition: closed by evidence, kept for cadence review
  - risk: MigrateAsync with an explicit target migration throws not-found on MySQL although the migration is listed (production uses EnsureCreated, so no production impact; targeted downgrade path unproven)
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Owner
    happy_path: frontend/tests/e2e/iam-happy-path.spec.js
  - role: Builder
    happy_path: frontend/tests/e2e/iam-builder.spec.js
owner_unit_assignment_registration:
  journey: An Owner registers only after a builder assigns an existing unit to their email.
  actor_coverage:
    - actor: Owner with assigned unit
      outcome: Registration completes; existing auto-linking attaches the account to the unit.
    - actor: Owner without assigned unit
      outcome: Frontend disables Next and backend rejects direct registration with 403.
    - actor: Builder
      outcome: Registration remains available without a unit assignment.
  scenarios:
    - tier: A
      scenario: Unassigned Owner registration is rejected before user/dispatch persistence.
      owner: backend/tests/Modules/IamWorkflowTests.cs
    - tier: A
      scenario: Both public registration routes reject unassigned Owners; assigned-owner registration remains successful.
      owner: backend/tests/Modules/IamWorkflowTests.cs
    - tier: B
      scenario: Invitation lookup failure disables Next and blocks the Owner form from progressing.
      owner: frontend/tests/e2e/iam-form-feedback.spec.js
  layer_ownership:
    G0: RegisterUserWorkflow tests prove no user or dispatch is persisted without an assigned unit.
    G1: IamApiContractTests prove both public signup routes return the explicit 403 contract.
    G2: Playwright proves Owner rejection/assigned-unit success and Builder registration success against the running MySQL-backed stack.
    G3: Direct API bypass and invitation-service outage are both covered as fail-closed scenarios.
    G4: Final six-journey rerun passed against an isolated Docker Compose stack.
  convergence_points:
    - Invitation lookup assigned/unitId matches backend eligibility rule.
    - Rejected backend signup leaves login unauthorized for that email.
    - Existing Owner auto-linking succeeds when Unit.OwnerEmail or Client.UnitId associates a real unit.
  gates:
    G0: passed
    G1: passed
    G2: passed
    G3: passed
    G4: passed
  commands:
    - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
      result: 233/233 passed (17 architecture + 20 contract + 42 integration + 154 modules).
    - command: npm run test:unit
      result: 56/56 passed.
    - command: npm run build
      result: passed; existing Vite warning reports a vendor chunk slightly over 1 MB.
    - command: E2E_BASE_URL=http://localhost:8081 npm run test:e2e -- --grep "IAM Builder happy path|IAM happy path|ANALYTICS Owner|DEVICES Owner happy path|Owner registration requires an assigned unit|fails closed when unit assignment cannot be verified"
      result: 6/6 passed against isolated Docker Compose with MySQL 8, API, frontend, and Nginx.
    - command: E2E_BASE_URL=http://localhost:8081 npm run test:e2e -- --grep "IAM happy path|Owner registration requires an assigned unit|fails closed when unit assignment cannot be verified"
      result: 3/3 passed; E2E asserts the Next button is disabled and gray (rgb(156, 163, 175)) for missing assignments, and enabled for an assigned Owner.
  diagnostic_verdicts:
    - failure: Initial E2E locator matched visible/inactive step panels strictly.
      verdict: Test assertion issue; scoped visible-message assertion and hidden-step assertion fixed it; final five-test run passed.
    - failure: E2E attempted clicking Next after email blur had already started assignment lookup and disabled the button.
      verdict: Expected product behavior; tests now assert the lookup feedback and disabled state produced by blur.
  open_risks: []
```
