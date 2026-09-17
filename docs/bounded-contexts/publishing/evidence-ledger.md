# Publishing evidence ledger

```yaml
context: publishing
status: piloted
journey: PUBLISHING.MANAGE (Builder creates projects, defines structure once, manages units and clients)
feature: publishing-convergence
gates:
  G0: passed
  G1: partial
  G2: skipped
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G1
    reason: ownership proven at the API boundary on doubles; no persistence guarantees on the production engine yet
  - gate: G2
    reason: no Builder management E2E yet
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
commands:
  - command: dotnet test backend/tests/Modules --filter PublishingAccessTests
    result: 3/3 passed (project create/read/update/delete ownership, structure plus unit ownership, client ownership)
  - command: npm run test:unit (frontend)
    result: 36/36 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5 + publishing-contract 4/4, last local run)
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
  - risk: no Builder management E2E yet
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered: []
```
