# Subscriptions evidence ledger

```yaml
context: subscriptions
status: piloted
journey: SUBSCRIPTIONS.PURCHASE (Builder-only)
feature: subscriptions-convergence
gates:
  G0: passed
  G1: partial
  G2: skipped
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G1
    reason: checkout→confirm→invoices proven by local-only shell proof script (MySQL + fake Stripe), not by versioned tests; no persistence guarantees on the production engine yet
  - gate: G2
    reason: no Builder purchase E2E yet
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
commands:
  - command: dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --filter "FullyQualifiedName~StripeKeyDisciplineTests"
    result: 4/4 passed (restricted-key resolution, fail-closed secrets, outgoing Authorization header)
  - command: npm run test:unit (frontend)
    result: 21/21 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7, last local run)
artifacts: []
failures:
  - class: product
    evidence_for: [AuthorizedRequest replaced the restricted key with any configured sk_ secret on outgoing Stripe calls]
    evidence_against: [resolver gates rk_ correctly; business rule requires least privilege]
    verdict: leftover pre-discipline behavior silently defeating the rk_ rule; fixed to send the restricted key only, covered by the outgoing-header test
open_risks:
  - risk: checkout→confirm→invoices has no versioned executable proof (only the local shell proof script)
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: webhook path scheduled, not evidenced (needs real Stripe keys plus public URL)
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: no Builder purchase E2E yet
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Builder
    happy_path: missing (open risk)
```
