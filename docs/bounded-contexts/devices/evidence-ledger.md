# Devices evidence ledger

```yaml
context: devices
status: piloted
journey: DEVICES.CONTROL (Owner sends commands to own unit devices)
feature: devices-convergence
gates:
  G0: passed
  G1: partial
  G2: skipped
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G1
    reason: command authorization proven at service level (pre-existing IoT suite); no versioned API flow or MySQL guarantees for control yet
  - gate: G2
    reason: no Owner control E2E yet
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
commands:
  - command: dotnet test backend/tests/Modules --filter DeviceManageTests
    result: 3/3 passed (owner/builder manage own devices, foreign 404, anonymous 401)
  - command: npm run test:unit (frontend)
    result: 32/32 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5, last local run)
artifacts: []
failures:
  - class: product
    evidence_for: [PUT and DELETE device endpoints required login but enforced zero ownership: any user could edit or delete any device]
    evidence_against: [create-custom path already checks Owner role plus unit ownership]
    verdict: unit-owner-or-project-builder management check on both endpoints (foreign reads as not found); list stays unfiltered by design, recorded as scheduled risk
open_risks:
  - risk: device list has no per-role visibility scoping (frontend fetches all and filters client-side)
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: no Owner control E2E yet
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered: []
```
