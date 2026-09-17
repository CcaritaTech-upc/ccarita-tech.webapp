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
  G3: partial
  G4: partial
skip_reasons:
  - gate: G3
    reason: Tier A complete; Tier B/C portfolios partial, Tier D has no fuzz/mutation campaigns
  - gate: G4
    reason: no flaky-rate or mutation tracking
commands:
  - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
    result: 139/139 passed without MySQL (opt-in tests skip-with-success; last full local run)
  - command: IOBUILD_TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=3306;Database=iobuild;User=root;Password=iobuild" dotnet test backend/IoBuild.sln --verbosity minimal (with a temporary 3306:3306 host mapping on mysql-monolith, reverted afterwards)
    result: 146/146 passed against live MySQL 8.0 (15 architecture + 20 contract + 41 integration + 70 modules, including Tier A escalation/canonical, concurrent duplicate x6 stress, and post-write rollback)
  - command: same full-solution run after removing stale pre-existing outbox rows (arroz/wasa/dbproof, owner-approved) with a quiet table
    result: 146/146 passed with every MySQL opt-in test fully executing, outbox table left at zero rows
  - command: npm run test:unit (frontend)
    result: 14/14 Vitest passed (validators 7/7 + iam-contract 7/7, last local run)
  - command: npx playwright test (frontend, E2E_BASE_URL=http://localhost:8081 against deployed nginx + dist + API + MySQL)
    result: 2/2 passed — iam-happy-path.spec.js (Owner register/login/logout/revoked-401) and iam-builder.spec.js (Builder register/login), last local run
  - command: npx playwright test (frontend, default vite dev 5173 proxied to stack API)
    result: 2/2 passed, last local run
  - command: dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --verbosity minimal
    result: 66/66 passed, including 3 new MySQL opt-in persistence tests (skip-with-success without IOBUILD_TEST_MYSQL_CONNECTION)
  - command: IOBUILD_TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=3306;Database=iobuild;User=root;Password=iobuild" dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --filter "FullyQualifiedName~IamPersistenceMySqlTests" (with a temporary 3306:3306 host mapping on mysql-monolith, reverted afterwards)
    result: 3/3 passed against live MySQL 8.0 (duplicate idempotency, fail-closed rollback, revocation durability across contexts)
  - command: npx playwright test (frontend/tests/e2e/iam-happy-path.spec.js)
    result: 1/1 passed against real stack, Owner only (last local run)
  - command: node scripts/verify-agent-compatibility.mjs
    result: Agent compatibility verified for 4 skills.
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
  - risk: frontend-e2e CI job added but not yet observed green on a real CI run
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: Tier B/C gaps per pilot portfolio (timeout safe-errors, conflicting tab sessions, migration survival, lock contention) and Tier D fuzz/mutation campaigns unscheduled
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Owner
    happy_path: frontend/tests/e2e/iam-happy-path.spec.js
  - role: Builder
    happy_path: frontend/tests/e2e/iam-builder.spec.js
```
