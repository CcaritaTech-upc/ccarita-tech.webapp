# IAM evidence ledger

```yaml
context: iam
status: piloted
journey: IAM.REGISTRATION → IAM.LOGIN → IAM.AUTHORIZED_ACCESS → IAM.LOGOUT
feature: iobuild-iam-convergence
gates:
  G0: passed
  G1: partial
  G2: partial
  G3: partial
  G4: partial
skip_reasons:
  - gate: G1
    reason: no executable frontend/API contract; IAM guarantees not proven against production MySQL
  - gate: G2
    reason: Builder journey has no E2E; E2E runs local-only, not in CI
  - gate: G3
    reason: Tier A/B/C portfolios incomplete, Tier D has no fuzz/mutation campaigns
  - gate: G4
    reason: no flaky-rate or mutation tracking
commands:
  - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
    result: 139/139 passed (last full local run)
  - command: npm run test:unit (frontend)
    result: 7/7 Vitest passed (last local run)
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
open_risks:
  - risk: Builder register/login journey has no E2E evidence
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: E2E does not run in CI
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: IAM persistence guarantees (rollback, concurrent duplicate, revocation durability) unproven on production MySQL
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: Tier A gaps (privilege escalation, registration rollback, concurrent duplicate) and Tier B/C gaps per pilot portfolio
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Owner
    happy_path: frontend/tests/e2e/iam-happy-path.spec.js
  - role: Builder
    happy_path: missing (open risk)
```
